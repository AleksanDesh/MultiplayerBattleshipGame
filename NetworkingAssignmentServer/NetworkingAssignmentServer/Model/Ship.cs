using System.Numerics;

namespace Model
{
    public class Ship
    {
        public readonly Vector2 position;
        public readonly int length;
        public readonly bool rotated;
        public Ship(Vector2 pos, int len, bool rot)
        {
            position = pos;
            length = len;
            rotated = rot;
        }
    }
}
