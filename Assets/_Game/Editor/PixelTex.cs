using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Utilidades de dibujo pixel a pixel + guardado como sprite con los
    /// ajustes de importacion correctos (filtro Point, sin compresion, bordes
    /// de 9-slice). Todo el marco de interfaz del menu se genera con esto.
    /// </summary>
    public static class PixelTex
    {
        public static Texture2D New(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color32(255, 255, 255, 0);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            tex.SetPixels32(px);
            return tex;
        }

        public static void Px(Texture2D t, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= t.width || y >= t.height) return;
            t.SetPixel(x, y, c);
        }

        public static Color Get(Texture2D t, int x, int y)
        {
            if (x < 0 || y < 0 || x >= t.width || y >= t.height) return new Color(0, 0, 0, 0);
            return t.GetPixel(x, y);
        }

        public static void FillRect(Texture2D t, int x, int y, int w, int h, Color c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Px(t, i, j, c);
        }

        public static void Fill(Texture2D t, Color c) => FillRect(t, 0, 0, t.width, t.height, c);

        public static void RectOutline(Texture2D t, int x, int y, int w, int h, int thickness, Color c)
        {
            for (int k = 0; k < thickness; k++)
            {
                FillRect(t, x + k, y + k, w - k * 2, 1, c);
                FillRect(t, x + k, y + h - 1 - k, w - k * 2, 1, c);
                FillRect(t, x + k, y + k, 1, h - k * 2, c);
                FillRect(t, x + w - 1 - k, y + k, 1, h - k * 2, c);
            }
        }

        public static void Line(Texture2D t, int x0, int y0, int x1, int y1, Color c, int thickness = 1)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if (thickness <= 1) Px(t, x0, y0, c);
                else FillRect(t, x0 - thickness / 2, y0 - thickness / 2, thickness, thickness, c);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public static void Disc(Texture2D t, float cx, float cy, float r, Color c)
        {
            int min = Mathf.FloorToInt(Mathf.Min(cx - r, cy - r)) - 1;
            int maxX = Mathf.CeilToInt(cx + r) + 1;
            int maxY = Mathf.CeilToInt(cy + r) + 1;
            for (int y = min; y <= maxY; y++)
                for (int x = min; x <= maxX; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r) Px(t, x, y, c);
                }
        }

        public static void Ring(Texture2D t, float cx, float cy, float rOuter, float rInner, Color c)
        {
            int maxR = Mathf.CeilToInt(rOuter) + 1;
            for (int y = Mathf.FloorToInt(cy) - maxR; y <= cy + maxR; y++)
                for (int x = Mathf.FloorToInt(cx) - maxR; x <= cx + maxR; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 <= rOuter * rOuter && d2 >= rInner * rInner) Px(t, x, y, c);
                }
        }

        /// <summary>
        /// Baja el alfa de los pixeles interiores dejando el contorno a tope:
        /// da el aspecto de "pixel art con linea de contorno".
        /// </summary>
        public static void InnerShade(Texture2D t, float innerAlphaScale)
        {
            int w = t.width, h = t.height;
            var src = t.GetPixels();
            var dst = new Color[src.Length];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = src[i];
                    if (c.a <= 0.01f) { dst[i] = c; continue; }

                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1 && !edge; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) { edge = true; break; }
                            if (src[ny * w + nx].a <= 0.01f) edge = true;
                        }

                    if (!edge) c.a *= innerAlphaScale;
                    dst[i] = c;
                }

            t.SetPixels(dst);
        }

        // ------------------------------------------------------------ guardar

        // 100 = mismo valor que "Reference Pixels Per Unit" del CanvasScaler,
        // asi 1 texel del sprite equivale a 1 pixel de interfaz y los bordes
        // de 9-slice no se escalan.
        public static Sprite Save(Texture2D tex, string folder, string name,
                                  Vector4 border = default, int pixelsPerUnit = 100,
                                  FilterMode filter = FilterMode.Point,
                                  TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            tex.Apply();
            Directory.CreateDirectory(folder);

            string path = $"{folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = filter;
                importer.wrapMode = wrap;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePixelsPerUnit = pixelsPerUnit;
                settings.wrapMode = wrap;
                settings.filterMode = filter;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
