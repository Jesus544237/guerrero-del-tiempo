using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Renderiza la camara principal a un PNG sin entrar en modo juego. Sirve
    /// para revisar de un vistazo como esta quedando el encuadre de un nivel.
    /// </summary>
    public static class SceneCapture
    {
        const string OutPath = "Tools/out/captura_escena.png";

        [MenuItem("Ekkar/Debug/Capturar camara", priority = 90)]
        public static void Capture()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Ekkar] No hay camara con la etiqueta MainCamera en la escena.");
                return;
            }

            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Point,
            };

            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            string full = Path.GetFullPath(OutPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log($"[Ekkar] Captura guardada en {OutPath}");
        }
    }
}
