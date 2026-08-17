using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Anade (o actualiza) un telon de relleno detras de todo, colgado de la
    /// camara.
    ///
    /// El sprite del cielo solo es opaco en su 72% superior, y sus dos
    /// extremos son de colores muy distintos (#19284C arriba, #0D0717 abajo),
    /// asi que ningun color de fondo unico sirve: si se ajusta al de arriba
    /// aparece una franja clara por debajo del cielo, y al reves. Este telon
    /// es un degradado sacado del propio cielo, de modo que cualquier hueco
    /// se rellena con el color que le corresponde a esa altura.
    ///
    /// Es aditivo: no reconstruye la escena ni toca la colocacion de las
    /// capas, solo cuelga un objeto mas de la camara.
    /// </summary>
    public static class SkyBackdropFix
    {
        const string SpritePath = "Assets/_Game/Art/Backgrounds/Medieval/bg_cielo_relleno.png";
        const string ObjectName = "FondoDeRelleno";

        [MenuItem("Ekkar/Niveles/Anadir fondo de relleno", priority = 41)]
        public static void AddBackdrop()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Ekkar] No hay camara con la etiqueta MainCamera en la escena abierta.");
                return;
            }

            var sprite = LoadSprite();
            if (sprite == null) return;

            var old = cam.transform.Find(ObjectName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(ObjectName, typeof(SpriteRenderer));
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 40f);

            float height = cam.orthographic ? cam.orthographicSize * 2f : 10.8f;
            float width = height * (16f / 9f);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(width + 2f, height + 2f);
            sr.sortingOrder = -500;          // por detras del cielo (-100)

            // el color de fondo pasa a ser el del extremo inferior del cielo,
            // por si el telon no llegara a cubrir algo
            cam.backgroundColor = new Color(13f / 255f, 6f / 255f, 23f / 255f, 1f);

            EditorUtility.SetDirty(cam.gameObject);
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            EditorSceneManager.SaveScene(cam.gameObject.scene);

            Debug.Log($"[Ekkar] Telon de relleno anadido bajo {cam.name} ({sr.size.x:0.0} x {sr.size.y:0.0}).");
        }

        static Sprite LoadSprite()
        {
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Ekkar] Falta {SpritePath}. Ejecuta antes:\n" +
                               "    python Tools/prepare_medieval_layers.py");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;   // degradado suave, no pixel
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // necesario para drawMode Sliced
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }
    }
}
