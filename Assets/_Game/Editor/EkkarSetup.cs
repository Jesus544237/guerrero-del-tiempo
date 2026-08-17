using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Menu "Ekkar" del editor: importa lo que hace falta, genera el arte y
    /// construye la escena del menu principal.
    /// </summary>
    public static class EkkarSetup
    {
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        const string TmpFolder = "Assets/TextMesh Pro";
        const string ArtFolder = "Assets/_Game/Art";

        [MenuItem("Ekkar/Menu Principal/1 - Importar recursos de TextMeshPro", false, 10)]
        public static void ImportTextMeshPro()
        {
            if (Directory.Exists(TmpFolder))
            {
                Debug.Log("[Ekkar] Los recursos de TextMeshPro ya estaban importados.");
                return;
            }

            string full = Path.GetFullPath(TmpEssentials);
            if (!File.Exists(full))
            {
                Debug.LogError($"[Ekkar] No encuentro el paquete de TMP en {TmpEssentials}. " +
                               "Importalo a mano desde Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            Debug.Log("[Ekkar] Importando recursos esenciales de TextMeshPro...");
            AssetDatabase.ImportPackage(full, false);
        }

        [MenuItem("Ekkar/Menu Principal/2 - Construir menu principal", false, 11)]
        public static void BuildMainMenu()
        {
            if (!Directory.Exists(TmpFolder))
            {
                Debug.LogError("[Ekkar] Faltan los recursos de TextMeshPro. Ejecuta antes el paso 1.");
                return;
            }

            ConfigureArtImports();
            MenuArtGenerator.GenerateAll();
            EkkarFontBuilder.BuildFontAsset(true);
            ApplyPlayerSettings();
            MainMenuBuilder.Build();
        }

        [MenuItem("Ekkar/Menu Principal/Regenerar solo el arte de interfaz", false, 30)]
        public static void RegenerateArt()
        {
            MenuArtGenerator.GenerateAll();
            Debug.Log("[Ekkar] Arte de interfaz regenerado.");
        }

        [MenuItem("Ekkar/Menu Principal/Reajustar importacion de sprites", false, 31)]
        public static void ConfigureArtImports()
        {
            // Personaje: pixel nitido, sin filtrado
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder + "/Characters" }))
                ConfigureSprite(AssetDatabase.GUIDToAssetPath(guid), FilterMode.Point, 2048);

            // Ilustracion de fondo: se escala con Ken Burns, mejor bilineal
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder + "/Backgrounds" }))
                ConfigureSprite(AssetDatabase.GUIDToAssetPath(guid), FilterMode.Bilinear, 2048);

            AssetDatabase.Refresh();
        }

        static void ConfigureSprite(string path, FilterMode filter, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            // Las hojas de animacion se quedan como estan. Son de los
            // importadores de Ekkar y de los enemigos, que las trocean en
            // fotogramas (modo Multiple) y guardan cada recorte con su nombre.
            // Pasarlas a Single borra ese troceo de golpe, y como los clips de
            // animacion apuntan a los recortes, todos los personajes se quedan
            // sin sprite y desaparecen del nivel.
            if (importer.spriteImportMode == SpriteImportMode.Multiple) return;

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.filterMode != filter) { importer.filterMode = filter; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; dirty = true; }
            if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; dirty = true; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }

            if (dirty) importer.SaveAndReimport();
        }

        [MenuItem("Ekkar/Menu Principal/Aplicar ajustes de Player (1920x1080)", false, 32)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Guerrero del Tiempo";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[Ekkar] Player Settings ajustados a 1920x1080 / 16:9.");
        }

        [MenuItem("Ekkar/Menu Principal/Abrir escena del menu", false, 50)]
        public static void OpenMenuScene()
        {
            if (File.Exists(MainMenuBuilder.ScenePath))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            else
                Debug.LogWarning("[Ekkar] Todavia no existe la escena del menu. Ejecuta el paso 2.");
        }
    }
}
