using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Importa las hojas de sprites que genera Tools/build_ekkar_sheets.py:
    /// las trocea en rejilla, coloca el pivote en el punto de apoyo del
    /// personaje, crea un AnimationClip por animacion y monta un
    /// AnimatorController con todos los estados.
    /// </summary>
    public static class EkkarSpriteImporter
    {
        const string AnimFolder = "Assets/_Game/Art/Characters/Ekkar/Anim";
        const string ClipFolder = "Assets/_Game/Animation/Ekkar";
        const string ControllerPath = ClipFolder + "/Ekkar.controller";
        const string ManifestPath = AnimFolder + "/ekkar_anim_manifest.json";

        const int PixelsPerUnit = 100;

        [System.Serializable]
        public class AnimEntry
        {
            public string name;
            public string file;
            public int frames;
            public int cols;
            public int rows;
            public int cellWidth;
            public int cellHeight;
            public float pivotX;
            public float pivotY;
            public int fps;
            public bool loop;
        }

        [System.Serializable]
        public class Manifest
        {
            public float scale;
            public AnimEntry[] animations;
        }

        [MenuItem("Ekkar/Personaje/Importar animaciones de Ekkar", priority = 20)]
        public static void ImportAll()
        {
            var manifest = LoadManifest();
            if (manifest == null) return;

            Directory.CreateDirectory(ClipFolder);

            var clips = new List<AnimationClip>();
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < manifest.animations.Length; i++)
                {
                    var entry = manifest.animations[i];
                    EditorUtility.DisplayProgressBar("Importando Ekkar",
                        entry.name, i / (float)manifest.animations.Length);
                    SliceSheet(entry);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            foreach (var entry in manifest.animations)
            {
                var clip = BuildClip(entry);
                if (clip != null) clips.Add(clip);
            }

            BuildController(clips);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Ekkar] {clips.Count} animaciones importadas en {ClipFolder}");
        }

        static Manifest LoadManifest()
        {
            string full = Path.GetFullPath(ManifestPath);
            if (!File.Exists(full))
            {
                Debug.LogError($"[Ekkar] Falta {ManifestPath}. Ejecuta antes:\n" +
                               "    python Tools/build_ekkar_sheets.py");
                return null;
            }

            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(full));
            if (manifest?.animations == null || manifest.animations.Length == 0)
            {
                Debug.LogError("[Ekkar] El manifiesto no tiene animaciones.");
                return null;
            }
            return manifest;
        }

        // ------------------------------------------------------------ troceo

        static void SliceSheet(AnimEntry entry)
        {
            string path = $"{AnimFolder}/{entry.file}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Ekkar] No encuentro la hoja {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            int sheetHeight = entry.rows * entry.cellHeight;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            for (int i = 0; i < entry.frames; i++)
            {
                int col = i % entry.cols;
                int row = i / entry.cols;

                rects.Add(new SpriteRect
                {
                    name = $"{Path.GetFileNameWithoutExtension(entry.file)}_{i:00}",
                    spriteID = GUID.Generate(),
                    // Unity mide Y desde abajo; la rejilla se genero desde arriba
                    rect = new Rect(col * entry.cellWidth,
                                    sheetHeight - (row + 1) * entry.cellHeight,
                                    entry.cellWidth, entry.cellHeight),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(entry.pivotX, entry.pivotY),
                    border = Vector4.zero,
                });
            }

            provider.SetSpriteRects(rects.ToArray());

            // Unity 2021+ exige mantener el mapa nombre -> fileId
            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
            {
                var pairs = rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList();
                nameIds.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------- clips

        static AnimationClip BuildClip(AnimEntry entry)
        {
            string path = $"{AnimFolder}/{entry.file}";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                                       .OfType<Sprite>()
                                       .OrderBy(s => s.name, System.StringComparer.Ordinal)
                                       .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogWarning($"[Ekkar] {entry.name}: la hoja no genero sprites.");
                return null;
            }

            float fps = Mathf.Max(1, entry.fps);
            var clip = new AnimationClip { frameRate = fps };

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = entry.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = $"{ClipFolder}/{entry.name}.anim";
            clip.name = entry.name;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                existing.name = entry.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(clip);
                return existing;
            }

            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        // -------------------------------------------------------- controlador

        static void BuildController(List<AnimationClip> clips)
        {
            if (clips.Count == 0) return;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states.ToArray())
                machine.RemoveState(state.state);

            AnimatorState idle = null;
            float x = 260f, y = 60f;
            int i = 0;
            foreach (var clip in clips.OrderBy(c => c.name, System.StringComparer.Ordinal))
            {
                var state = machine.AddState(clip.name, new Vector3(x + (i % 2) * 260f, y + (i / 2) * 70f, 0f));
                state.motion = clip;
                state.writeDefaultValues = false;
                if (clip.name == "idle") idle = state;
                i++;
            }

            if (idle != null) machine.defaultState = idle;

            EditorUtility.SetDirty(controller);
        }
    }
}
