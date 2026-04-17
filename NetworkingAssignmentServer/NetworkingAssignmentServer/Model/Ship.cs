using System.Numerics;

namespace Model
{
    public class Ship
    {
        public readonly int Id;
        public readonly Vector2 position;
        public readonly int length;
        public readonly bool rotated;
        public Ship(Vector2 pos, int len, bool rot, int id)
        {
            position = pos;
            length = len;
            rotated = rot;
            Id = id;
        }
    }
}
