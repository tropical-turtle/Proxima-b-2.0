﻿using Proxima.Core.Evaluation.Castling;
using Proxima.Core.Evaluation.KingSafety;
using Proxima.Core.Evaluation.Material;
using Proxima.Core.Evaluation.Mobility;
using Proxima.Core.Evaluation.PawnStructure;
using Proxima.Core.Evaluation.Position;

namespace Proxima.Core.Evaluation
{
    public class EvaluationResult
    {
        public MaterialResult Material { get; set; }
        public MobilityResult Mobility { get; set; }
        public CastlingResult Castling { get; set; }
        public PositionResult Position { get; set; }
        public PawnStructureResult PawnStructure { get; set; }
        public KingSafetyResult KingSafety;

        public int Total
        {
            get
            {
                return Material.Difference +
                       Mobility.Difference +
                       Castling.Difference +
                       Position.Difference +
                       PawnStructure.Difference + 
                       KingSafety.Difference;
            }
        }
    }
}