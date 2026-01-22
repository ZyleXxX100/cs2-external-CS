using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace Chrome2
{
    public static class Calculate
    {
        public static Vector2 WorldToScreen(float[] matrix, Vector3 pos, Vector2 windowSize)
        {
            // Use the common DirectX-style mapping (column-major order)
            float w = matrix[3] * pos.X + matrix[7] * pos.Y + matrix[11] * pos.Z + matrix[15];

            if (w <= 0.001f)
                return Vector2.Zero; // off-screen

            float x = matrix[0] * pos.X + matrix[4] * pos.Y + matrix[8] * pos.Z + matrix[12];
            float y = matrix[1] * pos.X + matrix[5] * pos.Y + matrix[9] * pos.Z + matrix[13];

            float nx = x / w;
            float ny = y / w;

            float screenX = (windowSize.X / 2f) * (nx + 1f);
            float screenY = (windowSize.Y / 2f) * (1f - ny);

            return new Vector2(screenX, screenY);
        }
    }
}