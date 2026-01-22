using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Vortice.Mathematics;
using SixLabors.ImageSharp.Metadata;

namespace Chrome2
{
    public class Entity
    {
        public Vector3 postion { get; set; }
        public Vector3 viewOffset { get; set; }

        public Vector2 position2D { get; set; }
        public Vector2 viewPosition2D { get; set; }

        public int team { get; set; }

    }
}