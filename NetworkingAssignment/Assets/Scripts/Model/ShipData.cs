using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using UnityEngine.UIElements;

namespace Model
{
    public class ShipData
    {
        public readonly Vector2 position;
        public readonly int length;
        public readonly bool rotated;
        public ShipData(Vector2 pos, int len, bool rot)
        {
            position = pos;
            length = len;
            rotated = rot;
        }
    }
}
