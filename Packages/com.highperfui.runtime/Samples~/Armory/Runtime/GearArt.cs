using UnityEngine;

namespace HighPerfUI.Reference
{
    // Original raster game assets. Shared by all benchmark modes; no third-party art dependency.
    public static class GearArt
    {
        public static Sprite Create(int kind)
        {
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color32[n * n];
            Color steel = UiFactory.Hex("A6C8CB"), light = UiFactory.Hex("E0EEDE"), dark = UiFactory.Hex("435E62"), gold = UiFactory.Hex("DFBC71"), red = UiFactory.Hex("AE5160");
            System.Action<Vector2[], Color> poly = (vertices, color) =>
            {
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    {
                        var a = vertices[i]; var b = vertices[j];
                        if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    }
                    if (inside) pixels[y * n + x] = color * Mathf.Lerp(.72f, 1.12f, y / 128f);
                }
            };
            System.Action<float[], Color> shape = (points, color) => { var v = new Vector2[points.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = new Vector2(points[i * 2], points[i * 2 + 1]); poly(v, color); };
            switch (kind)
            {
                case 0:
                    shape(new float[] { 55, 30, 53, 99, 64, 120, 75, 99, 73, 30 }, steel);
                    shape(new float[] { 64, 35, 64, 118, 72, 98, 71, 35 }, light);
                    shape(new float[] { 30, 31, 32, 42, 57, 45, 71, 45, 96, 42, 98, 31, 67, 37, 61, 37 }, gold);
                    shape(new float[] { 58, 7, 57, 34, 70, 34, 69, 7 }, red);
                    shape(new float[] { 54, 5, 57, 14, 70, 14, 75, 5, 64, 1 }, gold); break;
                case 1:
                    shape(new float[] { 24, 33, 29, 83, 47, 104, 78, 104, 99, 80, 105, 33, 89, 17, 38, 17 }, steel);
                    shape(new float[] { 63, 101, 82, 89, 93, 36, 67, 26 }, dark);
                    shape(new float[] { 22, 61, 57, 55, 59, 43, 26, 48 }, dark);
                    shape(new float[] { 68, 55, 105, 62, 101, 48, 68, 43 }, dark);
                    shape(new float[] { 58, 97, 69, 97, 69, 17, 58, 17 }, gold);
                    shape(new float[] { 59, 99, 57, 122, 73, 119, 69, 99 }, red); break;
                case 2:
                    shape(new float[] { 12, 82, 27, 105, 48, 102, 55, 88, 72, 88, 81, 102, 104, 105, 118, 82, 95, 66, 95, 14, 33, 14, 32, 66 }, steel);
                    shape(new float[] { 42, 81, 64, 64, 86, 81, 85, 29, 65, 18, 43, 29 }, dark);
                    shape(new float[] { 57, 78, 70, 78, 78, 52, 65, 43, 51, 52 }, gold);
                    shape(new float[] { 30, 20, 97, 20, 94, 11, 34, 11 }, gold); break;
                case 3:
                    shape(new float[] { 31, 15, 28, 55, 38, 71, 36, 96, 47, 106, 56, 105, 57, 116, 68, 116, 73, 101, 82, 105, 95, 96, 99, 66, 82, 46, 84, 15 }, steel);
                    shape(new float[] { 41, 50, 48, 78, 84, 78, 86, 53, 69, 38 }, dark);
                    shape(new float[] { 32, 21, 82, 21, 86, 9, 28, 9 }, gold); break;
                case 4:
                    shape(new float[] { 43, 113, 87, 108, 81, 46, 101, 26, 111, 17, 110, 7, 32, 7, 29, 25, 41, 48 }, dark);
                    shape(new float[] { 44, 109, 82, 104, 77, 54, 48, 48 }, steel);
                    shape(new float[] { 41, 76, 82, 73, 81, 64, 42, 67 }, gold);
                    shape(new float[] { 36, 35, 72, 39, 104, 18, 104, 13, 34, 14 }, steel); break;
                case 6:
                    shape(new float[] { 8, 25, 19, 65, 44, 75, 68, 41, 54, 17 }, steel);
                    shape(new float[] { 45, 47, 59, 102, 91, 112, 116, 59, 94, 19, 65, 19 }, steel);
                    shape(new float[] { 59, 100, 76, 76, 64, 20, 94, 20, 114, 58, 90, 109 }, dark);
                    shape(new float[] { 21, 62, 41, 72, 46, 47, 24, 30 }, light); break;
                case 7:
                    shape(new float[] { 24, 26, 24, 95, 63, 119, 102, 95, 102, 26, 64, 9 }, steel);
                    shape(new float[] { 31, 31, 31, 91, 64, 111, 95, 90, 95, 31, 64, 17 }, dark);
                    shape(new float[] { 51, 52, 44, 44, 40, 31, 85, 31, 82, 45, 71, 52 }, gold);
                    shape(new float[] { 52, 61, 53, 84, 64, 94, 76, 83, 75, 60, 65, 51 }, gold); break;
                case 8:
                    shape(new float[] { 19, 22, 19, 75, 31, 98, 96, 98, 110, 75, 110, 22 }, dark);
                    shape(new float[] { 22, 24, 22, 65, 107, 65, 107, 24 }, steel);
                    shape(new float[] { 30, 94, 96, 94, 106, 72, 22, 72 }, red);
                    shape(new float[] { 22, 65, 108, 65, 108, 74, 22, 74 }, gold);
                    shape(new float[] { 57, 53, 73, 53, 73, 80, 57, 80 }, gold);
                    shape(new float[] { 61, 59, 69, 59, 69, 68, 61, 68 }, dark); break;
                default:
                    shape(new float[] { 24, 65, 39, 100, 64, 116, 94, 96, 106, 62, 95, 26, 64, 9, 34, 25 }, gold);
                    shape(new float[] { 34, 65, 45, 88, 64, 99, 83, 85, 95, 63, 81, 38, 62, 27, 45, 39 }, dark);
                    shape(new float[] { 64, 95, 88, 64, 63, 29, 40, 64 }, red);
                    shape(new float[] { 64, 95, 64, 32, 42, 64 }, light); break;
            }
            texture.SetPixels32(pixels); texture.Apply(); texture.name = "Original equipment " + kind;
            return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100);
        }
    }
}
