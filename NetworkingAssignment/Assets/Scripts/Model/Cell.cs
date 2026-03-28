using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    internal class Cell
    {
        public enum CellState
        {
            Empty,
            Ship,
            Mine,
            //Resource,
            Bombed
        }
        public CellState _state;
        public Cell()
        {
            _state = CellState.Empty;
        }
    }
}
