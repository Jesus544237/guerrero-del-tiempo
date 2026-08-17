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
    /// Importa las hojas de sprites de los enemigos que deja
    /// Tools/build_enemy_sheets.py: las trocea en rejilla, coloca el pivote en
    /// el punto de apoyo, crea un AnimationClip por animacion, monta un
    /// AnimatorController por enemigo y deja un prefab listo para arrastrar a
    /// la escena.
    ///
    /// Es el mismo camino que sigue Ekkar en EkkarSpriteImporter, con dos
    /// diferencias: aqui hay varios personajes, y cada uno trae su propio
    /// manifiesto con su escala y su anclaje.
    /// </summary>
    public static class EnemySpriteImporter
    {
        const string ArtRoot = "Assets/_Game/Art/Characters/Enemigos";
        const string IndexPath = ArtRoot + "/enemigos_manifest.json";
        const string ClipRoot = "Assets/_Game/Animation/Enemigos";
        const string PrefabRoot = "Assets/_Game/Prefabs/Enemigos";

        const int PixelsPerUnit = 100;

        // ------------------------------------------------------------ modelo

        [System.Serializable]
        public class IndexEntry
        {
            public string name;
            public string era;
            public string folder;
            public string manifest;
            public int targetHeight;
            public string anchor;
        }

        [System.Serializable]
        public class Index
        {
            public IndexEntry[] enemies;
        }

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
            public string sound;
        }

        [System.Serializable]
        public class EnemyManifest
        {
            public string enemy;
            public string era;
            public int targetHeight;
            public string anchor;
            public float scale;
            public AnimEntry[] animations;
        }

        // ------------------------------------------------------------- menus

        [MenuItem("Ekkar/Enemigos/Importar animaciones de enemigos", priority = 40)]
        public static void ImportAll()
        {
            var index = LoadIndex();
            if (index == null) return;

            int hechos = 0;
            try
            {
                for (int i = 0; i < index.enemies.Length; i++)
                {
                    var e = index.enemies[i];
                    EditorUtility.DisplayProgressBar("Importando enemigos", e.name,
                        i / (float)index.enemies.Length);
                    if (ImportOne(e)) hechos++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Ekkar] {hechos} enemigos importados. Prefabs en {PrefabRoot}");
        }

        // ------------------------------------------------------------ lectura

        static Index LoadIndex()
        {
            string full = Path.GetFullPath(IndexPath);
            if (!File.Exists(full))
            {
                Debug.LogError($"[Ekkar] Falta {IndexPath}. Ejecuta antes:\n" +
                               "    python Tools/build_enemy_sheets.py");
                return null;
            }

            var index = JsonUtility.FromJson<Index>(File.ReadAllText(full));
            if (index?.enemies == null || index.enemies.Length == 0)
            {
                Debug.LogError("[Ekkar] El indice de enemigos esta vacio.");
                return null;
            }
            return index;
        }

        static bool ImportOne(IndexEntry entry)
        {
            string manifestPath = $"{ArtRoot}/{entry.manifest}";
            string full = Path.GetFullPath(manifestPath);
            if (!File.Exists(full))
            {
                Debug.LogWarning($"[Ekkar] {entry.name}: falta {manifestPath}");
                return false;
            }

            var manifest = JsonUtility.FromJson<EnemyManifest>(File.ReadAllText(full));
            if (manifest?.animations == null || manifest.animations.Length == 0)
            {
                Debug.LogWarning($"[Ekkar] {entry.name}: manifiesto sin animaciones");
                return false;
            }

            string artDir = $"{ArtRoot}/{entry.folder}";
            string clipDir = $"{ClipRoot}/{entry.name}";
            Directory.CreateDirectory(clipDir);
            Directory.CreateDirectory(PrefabRoot);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var anim in manifest.animations)
                    SliceSheet($"{artDir}/{anim.file}", anim);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();

            var clips = new List<AnimationClip>();
            foreach (var anim in manifest.animations)
            {
                var clip = BuildClip($"{artDir}/{anim.file}", anim, clipDir);
                if (clip != null) clips.Add(clip);
            }
            if (clips.Count == 0) return false;

            var controller = BuildController(clips, $"{clipDir}/{entry.name}.controller");
            BuildPrefab(entry, manifest, controller, artDir);
            return true;
        }

        // ------------------------------------------------------------- troceo

        static void SliceSheet(string path, AnimEntry entry)
        {
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
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            int sheetHeight = entry.rows * entry.cellHeight;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            string baseName = Path.GetFileNameWithoutExtension(entry.file);
            var rects = new List<SpriteRect>();
            for (int i = 0; i < entry.frames; i++)
            {
                int col = i % entry.cols;
                int row = i / entry.cols;

                rects.Add(new SpriteRect
                {
                    name = $"{baseName}_{i:00}",
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

            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
                nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

            provider.Apply();
            importer.SaveAndReimport();
        }

        // -------------------------------------------------------------- clips

        static AnimationClip BuildClip(string path, AnimEntry entry, string clipDir)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                                       .OfType<Sprite>()
                                       .OrderBy(s => s.name, System.StringComparer.Ordinal)
                                       .ToArray();
            if (sprites.Length == 0)
            {
                Debug.LogWarning($"[Ekkar] {entry.name}: la hoja no genero sprites ({path})");
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

            string clipPath = $"{clipDir}/{entry.name}.anim";
            clip.name = entry.name;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                // CopySerialized copia TODO, incluido el nombre. Si el clip
                // nuevo llega sin nombre, borra el del asset y el controlador
                // acaba con estados sin nombre que nadie puede reproducir.
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

        static AnimatorController BuildController(List<AnimationClip> clips, string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states.ToArray())
                machine.RemoveState(state.state);

            // Sin transiciones: el estado se fuerza desde el codigo con
            // Animator.Play, igual que hace EkkarController.
            AnimatorState idle = null;
            int i = 0;
            foreach (var clip in clips.OrderBy(c => c.name, System.StringComparer.Ordinal))
            {
                var state = machine.AddState(clip.name, new Vector3(260f + (i % 2) * 260f, 60f + (i / 2) * 70f, 0f));
                state.motion = clip;
                state.writeDefaultValues = false;
                if (clip.name == "idle") idle = state;
                i++;
            }

            if (idle != null) machine.defaultState = idle;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------ prefab

        static void BuildPrefab(IndexEntry entry, EnemyManifest manifest,
                                AnimatorController controller, string artDir)
        {
            var root = new GameObject(entry.name);

            var body = new GameObject("Sprite", typeof(SpriteRenderer), typeof(Animator));
            body.transform.SetParent(root.transform, false);

            var sr = body.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 1;

            var idleAnim = manifest.animations.FirstOrDefault(a => a.name == "idle")
                           ?? manifest.animations[0];
            var first = AssetDatabase.LoadAllAssetsAtPath($"{artDir}/{idleAnim.file}")
                                     .OfType<Sprite>()
                                     .OrderBy(s => s.name, System.StringComparer.Ordinal)
                                     .FirstOrDefault();
            if (first != null) sr.sprite = first;
            body.GetComponent<Animator>().runtimeAnimatorController = controller;

            // cuerpo: alto real del bicho, ancho aproximado, apoyado en y = 0
            float alto = manifest.targetHeight / (float)PixelsPerUnit;
            float ancho = Mathf.Max(0.4f, alto * 0.38f);

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;

            var col = root.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(ancho, alto);
            col.offset = new Vector2(0f, alto * 0.5f);

            string prefabPath = $"{PrefabRoot}/{entry.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
