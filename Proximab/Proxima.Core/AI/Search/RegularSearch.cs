﻿using System;
using System.Collections.Generic;
using System.Linq;
using Proxima.Core.AI.HistoryHeuristic;
using Proxima.Core.AI.KillerHeuristic;
using Proxima.Core.AI.Patterns;
using Proxima.Core.AI.SEE;
using Proxima.Core.AI.Transposition;
using Proxima.Core.Boards;
using Proxima.Core.Commons.Colors;
using Proxima.Core.MoveGenerators.Moves;

namespace Proxima.Core.AI.Search
{
    public class RegularSearch : SearchBase
    {
        private TranspositionTable _transpositionTable;
        private HistoryTable _historyTable;
        private KillerTable _killerTable;
        private QuiescenceSearch _quiescenceSearch;
        private PatternsDetector _patternsDetector;

        public RegularSearch(TranspositionTable transpositionTable, HistoryTable historyTable, KillerTable killerTable)
        {
            _transpositionTable = transpositionTable;
            _historyTable = historyTable;
            _killerTable = killerTable;
            _quiescenceSearch = new QuiescenceSearch();
            _patternsDetector = new PatternsDetector();
        }

        /// <summary>
        /// Temporary method to calculating best move.
        /// </summary>
        /// <param name="color">The player color.</param>
        /// <param name="bitboard">The bitboard.</param>
        /// <param name="depth">The current depth.</param>
        /// <param name="bestMove">The best possible move from nested nodes.</param>
        /// <param name="stats">The AI stats.</param>
        /// <returns>The evaluation score of best move.</returns>
        public int Do(Color color, Bitboard bitboard, int depth, int alpha, int beta, long deadline, AIStats stats)
        {
            var root = stats.TotalNodes == 0;

            var bestValue = AIConstants.InitialAlphaValue;
            var enemyColor = ColorOperations.Invert(color);
            var boardHash = bitboard.GetHashForColor(color);
            var originalAlpha = alpha;

            stats.TotalNodes++;

            if (bitboard.IsThreefoldRepetition())
            {
                stats.EndNodes++;
                return 0;
            }

            if (_transpositionTable.Exists(boardHash))
            {
                var transpositionNode = _transpositionTable.Get(boardHash);

                if (transpositionNode.Depth >= depth)
                {
                    stats.TranspositionTableHits++;
                    switch (transpositionNode.Type)
                    {
                        case ScoreType.Exact:
                        {
                            return transpositionNode.Score;
                        }

                        case ScoreType.LowerBound:
                        {
                            alpha = Math.Max(alpha, transpositionNode.Score);
                            break;
                        }

                        case ScoreType.UpperBound:
                        {
                            beta = Math.Min(beta, transpositionNode.Score);
                            break;
                        }
                    }

                    if (alpha >= beta)
                    {
                        return transpositionNode.Score;
                    }
                }
            }

            if (depth <= 0)
            {
                stats.EndNodes++;
                return _quiescenceSearch.Do(color, bitboard, alpha, beta, stats);
            }

            var whiteGeneratorMode = GetGeneratorMode(color, Color.White);
            var blackGeneratorMode = GetGeneratorMode(color, Color.Black);
            bitboard.Calculate(whiteGeneratorMode, blackGeneratorMode, false);

            if (bitboard.IsCheck(enemyColor))
            {
                stats.EndNodes++;
                return AIConstants.MateValue + depth;
            }

            Move bestMove = null;

            var availableMoves = SortMoves(color, depth, bitboard, bitboard.Moves);
            var firstMove = true;

            foreach (var move in availableMoves)
            {
                if (DateTime.Now.Ticks >= deadline)
                {
                    break;
                }

                if (root)
                {
                    if (_patternsDetector.IsPattern(bitboard, move))
                    {
                        continue;
                    }
                }

                var bitboardAfterMove = bitboard.Move(move);
                var nodeValue = 0;

                if (firstMove)
                {
                    nodeValue = -Do(enemyColor, bitboardAfterMove, depth - 1, -beta, -alpha, deadline, stats);
                    firstMove = false;
                }
                else
                {
                    nodeValue = -Do(enemyColor, bitboardAfterMove, depth - 1, -alpha - 1, -alpha, deadline, stats);

                    if (nodeValue > alpha && nodeValue < beta)
                    {
                        bitboardAfterMove = bitboard.Move(move);
                        nodeValue = -Do(enemyColor, bitboardAfterMove, depth - 1, -beta, -alpha, deadline, stats);
                    }
                }

                if (nodeValue > bestValue)
                {
                    bestValue = nodeValue;
                    bestMove = move;
                }

                alpha = Math.Max(nodeValue, alpha);

                if (alpha >= beta)
                {
                    if (move is QuietMove)
                    {
                        _historyTable.AddKiller(color, depth, bestMove);
                        _killerTable.SetKiller(color, depth, move);
                    }

                    stats.AlphaBetaCutoffs++;

                    break;
                }
            }

            if (bestValue == -(AIConstants.MateValue + depth - 1) && !bitboard.IsCheck(color))
            {
                stats.EndNodes++;
                return 0;
            }

            var updateTranspositionNode = new TranspositionNode();
            updateTranspositionNode.Score = bestValue;
            updateTranspositionNode.Depth = depth;
            updateTranspositionNode.BestMove = bestMove;

            if (bestValue <= originalAlpha)
            {
                updateTranspositionNode.Type = ScoreType.UpperBound;
            }
            else if (bestValue >= beta)
            {
                updateTranspositionNode.Type = ScoreType.LowerBound;
            }
            else
            {
                updateTranspositionNode.Type = ScoreType.Exact;
            }

            _transpositionTable.AddOrUpdate(boardHash, updateTranspositionNode);

            return bestValue;
        }

        private List<Move> SortMoves(Color color, int depth, Bitboard bitboard, LinkedList<Move> moves)
        {
            var sortedMoves = moves.Select(p => new RegularSortedMove { Move = p, Score = -100000 }).ToList();

            AssignSpecialScores(sortedMoves, color, depth);
            AssignSEEScores(color, bitboard, sortedMoves);
            AssignPVScore(color, bitboard, sortedMoves);

            return sortedMoves.OrderByDescending(p => p.Score).Select(p => p.Move).ToList();
        }

        private void AssignPVScore(Color color, Bitboard bitboard, List<RegularSortedMove> movesToSort)
        {
            var boardHash = bitboard.GetHashForColor(color);
            if (_transpositionTable.Exists(boardHash))
            {
                var transpositionNode = _transpositionTable.Get(boardHash);
                if (transpositionNode.BestMove != null)
                {
                    var pvMove = movesToSort.First(p =>
                        p.Move.From == transpositionNode.BestMove.From && p.Move.To == transpositionNode.BestMove.To);

                    pvMove.Score = 100000;
                }
            }
        }

        private void AssignSEEScores(Color color, Bitboard bitboard, List<RegularSortedMove> movesToSort)
        {
            var see = new SEECalculator();
            var seeResults = see.Calculate(color, bitboard);

            foreach (var seeResult in seeResults)
            {
                var sortedMove = movesToSort.FirstOrDefault(p => p.Move.From == seeResult.InitialAttackerFrom &&
                                                                 p.Move.To == seeResult.InitialAttackerTo);

                if (sortedMove != null)
                {
                    if (seeResult.Score < 0)
                    {
                        seeResult.Score *= 1000;
                    }

                    sortedMove.Score = seeResult.Score;
                }
            }
        }

        private void AssignSpecialScores(List<RegularSortedMove> movesToSort, Color color, int depth)
        {
            foreach (var move in movesToSort)
            {
                var killer = _historyTable.GetKillersCount(color, move.Move);
                if (move.Move is PromotionMove || move.Move is CastlingMove)
                {
                    move.Score = 50000;
                }
                else if (_killerTable.IsKiller(color, depth, move.Move))
                {
                    move.Score = 10;
                }
                else
                {
                    move.Score = -(1000 - killer);
                }
            }
        }
    }
}
