using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    internal class Mine
    {
        public readonly int Id;
        public readonly Vector2 position;

        public Mine(Vector2 position, int id)
        {
            this.position = position;
            this.Id = id;
        }
    }
}
