﻿using Core.Commons;
using System;

namespace GUI.Source.BoardSubsystem
{
    internal class FieldSelectedEventArgs : EventArgs
    {
        public Position Position { get; set; }
        public PieceType PieceType { get; set; }

        public FieldSelectedEventArgs(Position position, PieceType pieceType)
        {
            Position = position;
            PieceType = pieceType;
        }
    }
}
