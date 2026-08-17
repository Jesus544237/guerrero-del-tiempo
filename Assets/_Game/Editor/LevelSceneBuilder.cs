using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ekkar.Audio;
using Ekkar.Core;
using Ekkar.FX;
using Ekkar.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Constructor generico de niveles. Lee el level_manifest.json que deja
    /// Tools/prepare_level_layers.py y monta la escena entera: camara, capas
    /// de parallax ancladas por su contenido, suelo con colision, props,
    /// Ekkar, puntos de guardado, musica y particulas propias de la era.
    ///
    /// La Hora Cero se monta como arena cerrada en vez de recorrido lateral,
    /// y ademas recibe los telones de las tres eras para que el Senor del
    /// Tiempo pueda romper la realidad durante la fase 2.
    /// </summary>
    public static class LevelSceneBuilder
    {
        const string BG = "Assets/_Game/Art/Backgrounds";
        const string UiArt = "Assets/_Game/Art/UI/Generated";
        const string ControllerPath = "Assets/_Game/Animation/Ekkar/Ekkar.controller";

        const int PPU = 100;
        const float ScreenHeight = 10.8f;
        const float CameraY = 2.6f;

        // ------------------------------------------------------------ modelo

        enum Anchor { Center, ContentBottom }

        class LayerDef
        {
            public string key;
            public int order;
            public float follow;
            public Anchor anchor = Anchor.ContentBottom;
            public float y;            // centro (Center) o base del contenido (ContentBottom)
            public int tiles = 3;
            public bool atEnd;         // se coloca solo en el tramo final
            public float atX = float.NaN;  // si se indica, una sola copia en esa X del mundo
        }

        class LevelDef
        {
            public string era;         // carpeta bajo Backgrounds
            public string sceneName;
            public float length = 140f;
            public bool arena;
            public string bgColor;
            public LevelMusic.Era music;
            public LayerDef[] layers;
            public float switchX;      // 0 = sin relevo de telon
            public ParticleDef particles;
        }

        class ParticleDef
        {
            public int count = 110;
            public float reveal = 0f;
            public float emberRatio = 0.4f;
            public Vector2 fall = new Vector2(0.6f, 2.4f);
            public Vector2 size = new Vector2(0.03f, 0.11f);
            public float sway = 0.35f;
            public Color hot, cool, dust, accent;
        }

        static readonly LevelDef[] Levels =
        {
            new LevelDef {
                era = "Industrial", sceneName = "02_EraIndustrial_FundicionDeLasHoras",
                bgColor = "0E0618", music = LevelMusic.Era.Industrial, switchX = 56f,
                layers = new[] {
                    new LayerDef { key = "00_cielo",     order = -100, follow = 0.95f, anchor = Anchor.Center,        y = CameraY, tiles = 3 },
                    new LayerDef { key = "01_horizonte", order = -90,  follow = 0.72f, anchor = Anchor.ContentBottom, y = 0.10f,   tiles = 3 },
                    new LayerDef { key = "02_fabrica",   order = -80,  follow = 0.50f, anchor = Anchor.ContentBottom, y = -0.50f,  tiles = 1, atEnd = true },
                },
                particles = new ParticleDef {
                    count = 130, reveal = 0f, emberRatio = 0.5f,
                    fall = new Vector2(0.8f, 3.0f), sway = 0.25f,
                    hot = new Color(1f, 0.58f, 0.10f), cool = new Color(0.78f, 0.22f, 0.10f),
                    dust = new Color(0.42f, 0.40f, 0.46f), accent = new Color(0.98f, 0.75f, 0.14f),
                },
            },
            new LevelDef {
                era = "Futuro", sceneName = "03_FuturoDigital_NeonSinManana",
                bgColor = "150A2F", music = LevelMusic.Era.Futuro, switchX = 56f,
                layers = new[] {
                    new LayerDef { key = "00_cielo",     order = -100, follow = 0.95f, anchor = Anchor.Center,        y = CameraY, tiles = 3 },
                    new LayerDef { key = "01_horizonte", order = -90,  follow = 0.72f, anchor = Anchor.ContentBottom, y = 0.10f,   tiles = 3 },
                    new LayerDef { key = "02_servidor",  order = -80,  follow = 0.50f, anchor = Anchor.ContentBottom, y = -0.80f,  tiles = 1, atEnd = true },
                },
                particles = new ParticleDef {
                    count = 150, reveal = 0f, emberRatio = 0.30f,
                    fall = new Vector2(1.6f, 4.5f), sway = 0.06f,
                    size = new Vector2(0.02f, 0.09f),
                    hot = new Color(0.13f, 0.90f, 0.95f), cool = new Color(0.85f, 0.20f, 0.75f),
                    dust = new Color(0.35f, 0.42f, 0.60f), accent = new Color(0.55f, 1f, 0.85f),
                },
            },
            new LevelDef {
                // Arena de tres pantallas: los tres telones van uno al lado del
                // otro y fijos en el mundo, asi que Ekkar los recorre andando y
                // los tres llegan a verse. El vacio de detras si acompana a la
                // camara, que es lo que da la profundidad.
                era = "HoraCero", sceneName = "04_HoraCero_VacioEntreSegundos",
                length = 58f, arena = true,
                bgColor = "1D113F", music = LevelMusic.Era.HoraCero,
                layers = new[] {
                    new LayerDef { key = "00_vacio",   order = -100, follow = 0.95f, anchor = Anchor.Center,        y = CameraY, tiles = 4 },
                    new LayerDef { key = "01_ecos",    order = -86,  follow = 0f, anchor = Anchor.ContentBottom, y = 0.30f, atX = 9.6f },
                    new LayerDef { key = "02_trono",   order = -84,  follow = 0f, anchor = Anchor.Center,        y = 3.20f, atX = 28.9f },
                    new LayerDef { key = "01_2_ecos",  order = -86,  follow = 0f, anchor = Anchor.ContentBottom, y = 0.55f, atX = 48.2f },
                },
                particles = new ParticleDef {
                    count = 120, reveal = 0f, emberRatio = 0.45f,
                    fall = new Vector2(0.25f, 1.1f), sway = 0.5f,
                    size = new Vector2(0.04f, 0.14f),
                    hot = new Color(0.98f, 0.78f, 0.18f), cool = new Color(0.75f, 0.55f, 0.12f),
                    dust = new Color(0.30f, 0.30f, 0.45f), accent = new Color(0.13f, 0.86f, 0.95f),
                },
            },
        };

        // ------------------------------------------------------------ manifiesto

        [System.Serializable] class PropInfo { public string file; public int width, height; }
        [System.Serializable] class LayerInfo { public string key, file; public int width, height, contentTop, contentBottom; }
        [System.Serializable]
        class Manifest
        {
            public float artScale;
            public int groundTopPixel, groundHeight, groundSolidBottom;
            public LayerInfo[] layers;
            public PropInfo[] props;
        }

        // ------------------------------------------------------------ menus

        [MenuItem("Ekkar/Niveles/Construir Era Industrial", priority = 50)]
        public static void BuildIndustrial() => BuildOne("Industrial");

        [MenuItem("Ekkar/Niveles/Construir Futuro Digital", priority = 51)]
        public static void BuildFuturo() => BuildOne("Futuro");

        [MenuItem("Ekkar/Niveles/Construir Hora Cero", priority = 52)]
        public static void BuildHoraCero() => BuildOne("HoraCero");

        static void BuildOne(string era)
        {
            var def = Levels.FirstOrDefault(l => l.era == era);
            if (def == null) { Debug.LogError($"[Ekkar] Era desconocida: {era}"); return; }
            Build(def);
        }

        // ------------------------------------------------------------ build

        static void Build(LevelDef def)
        {
            string manifestPath = $"{BG}/{def.era}/level_manifest.json";
            string full = Path.GetFullPath(manifestPath);
            if (!File.Exists(full))
            {
                Debug.LogError($"[Ekkar] Falta {manifestPath}. Ejecuta antes:\n" +
                               "    python Tools/prepare_level_layers.py");
                return;
            }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(full));

            ConfigureImporters(def.era);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = BuildCamera(def);
            var fondo = new GameObject("Fondo").transform;
            var nivel = new GameObject("Nivel").transform;

            GameObject endGroup = null, midGroup = null;
            foreach (var layer in def.layers)
            {
                var info = manifest.layers.FirstOrDefault(l => l.key == layer.key);
                if (info == null) { Debug.LogWarning($"[Ekkar] {def.era}: falta la capa {layer.key}"); continue; }

                var go = BuildLayer(def, layer, info, fondo);
                if (layer.atEnd) endGroup = go;
                else if (layer.order == -90) midGroup = go;
            }

            BuildGround(def, manifest, nivel);
            BuildProps(def, manifest, nivel);

            var ekkar = BuildEkkar(def);
            cam.GetComponent<CameraFollow2D>().SetTarget(ekkar.transform);

            var overlays = BuildOverlays(def, cam.transform);
            BuildCheckpoints(def, nivel);
            BuildSystems(def);

            if (def.switchX > 0.1f && midGroup != null && endGroup != null)
                BuildSectionSwap(cam.transform, midGroup, endGroup, def.switchX);

            if (def.arena)
                BuildBossDirector(fondo, overlays);

            string path = $"Assets/_Game/Scenes/{def.sceneName}.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);

            Debug.Log($"[Ekkar] {def.era} construida en {path}");
        }

        static void ConfigureImporters(string era)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { $"{BG}/{era}" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool isProp = path.Contains("/Props/");
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.spritePixelsPerUnit = PPU;
                ti.maxTextureSize = 4096;
                ti.textureCompression = isProp
                    ? TextureImporterCompression.CompressedHQ
                    : TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static Camera BuildCamera(LevelDef def)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ScreenHeight * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.FromHex(def.bgColor);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            go.transform.position = new Vector3(0f, CameraY, -10f);
            go.AddComponent<UniversalAdditionalCameraData>();

            float half = ScreenHeight * 0.5f;
            var follow = go.AddComponent<CameraFollow2D>();
            var so = new SerializedObject(follow);
            so.FindProperty("minBounds").vector2Value = new Vector2(0f, CameraY - half);
            so.FindProperty("maxBounds").vector2Value = new Vector2(def.length, CameraY + half);
            so.ApplyModifiedPropertiesWithoutUndo();
            return cam;
        }

        /// <summary>Coloca una capa anclada por su contenido, no por su lienzo.</summary>
        static GameObject BuildLayer(LevelDef def, LayerDef layer, LayerInfo info, Transform parent)
        {
            var sprite = Load($"{BG}/{def.era}/{info.file}");
            if (sprite == null) { Debug.LogWarning($"[Ekkar] Falta {info.file}"); return null; }

            float h = info.height / (float)PPU;
            float w = info.width / (float)PPU;

            float centerY = layer.anchor == Anchor.Center
                ? layer.y
                : layer.y + h * 0.5f - (info.height - info.contentBottom) / (float)PPU;

            float firstX;
            int tiles = layer.tiles;
            if (!float.IsNaN(layer.atX))
            {
                // panel fijo en un punto del mundo: se recorre andando por delante
                firstX = layer.atX;
                tiles = 1;
            }
            else if (layer.atEnd)
                firstX = (def.length - 14f) * (1f - layer.follow);          // entra al final del nivel
            else
            {
                // Las copias tienen que cubrir todo el recorrido: la capa solo
                // avanza la fraccion (1 - follow) de lo que avanza la camara,
                // asi que ese es el ancho de mundo que hay que tapar. Se calcula
                // en vez de fijarlo, para que alargar el nivel no deje huecos.
                float screen = ScreenHeight * 16f / 9f;
                float span = def.length * (1f - layer.follow) + screen * 1.5f;
                tiles = Mathf.Max(layer.tiles, Mathf.CeilToInt(span / Mathf.Max(0.01f, w)) + 1);
                firstX = -w * (tiles - 1) * 0.5f;                           // centrada en el arranque
            }

            var root = new GameObject(LayerName(layer.key)).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0f, centerY, 0f);

            for (int i = 0; i < tiles; i++)
            {
                var tile = new GameObject($"{root.name}_{i}", typeof(SpriteRenderer));
                tile.transform.SetParent(root, false);
                tile.transform.localPosition = new Vector3(firstX + i * w, 0f, 0f);
                var sr = tile.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = layer.order;
            }

            var par = root.gameObject.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(par);
            so.FindProperty("factorX").floatValue = layer.follow;
            so.FindProperty("factorY").floatValue = layer.follow;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root.gameObject;
        }

        static string LayerName(string key)
        {
            switch (key)
            {
                case "00_cielo": return "01_Cielo";
                case "00_vacio": return "01_Vacio";
                case "01_horizonte": return "02_Horizonte";
                case "01_ecos": return "02_Ecos_A";
                case "01_2_ecos": return "03_Ecos_B";
                case "02_fabrica": return "03_Fabrica";
                case "02_servidor": return "03_Servidor";
                case "02_trono": return "04_Trono";
                default: return key;
            }
        }

        static void BuildGround(LevelDef def, Manifest m, Transform parent)
        {
            var info = m.layers.FirstOrDefault(l => l.key.Contains("suelo"));
            string file = info != null ? info.file : "bg_03_suelo.png";
            var sprite = Load($"{BG}/{def.era}/{file}");
            if (sprite == null) { Debug.LogWarning("[Ekkar] Falta la capa de suelo"); return; }

            float tileW = sprite.rect.width / PPU;
            float tileH = sprite.rect.height / PPU;
            float walkFromBottom = (m.groundHeight - m.groundTopPixel) / (float)PPU;
            float offsetY = -(walkFromBottom - tileH * 0.5f);

            var root = new GameObject("05_Suelo").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0f, offsetY, 0f);

            int tiles = Mathf.CeilToInt((def.length + tileW * 2f) / tileW);
            for (int i = 0; i < tiles; i++)
            {
                var go = new GameObject($"Suelo_{i}", typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3((i - 1) * tileW, 0f, 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = -10;
            }

            var col = new GameObject("Colision_Suelo");
            col.transform.SetParent(parent, false);
            col.transform.position = new Vector3(def.length * 0.5f, -2f, 0f);
            col.AddComponent<BoxCollider2D>().size = new Vector2(def.length + 40f, 4f);

            // muros dentro del alcance de la camara, para que Ekkar no se salga
            const float safe = 0.6f;
            foreach (var (x, name) in new[] { (safe - 0.5f, "Muro_Izq"), (def.length - safe + 0.5f, "Muro_Der") })
            {
                var wall = new GameObject(name);
                wall.transform.SetParent(parent, false);
                wall.transform.position = new Vector3(x, 4f, 0f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 14f);
            }
        }

        static void BuildProps(LevelDef def, Manifest m, Transform parent)
        {
            if (m.props == null || m.props.Length == 0) return;
            var sprites = m.props.Select(p => Load($"{BG}/{def.era}/Props/{p.file}")).Where(s => s != null).ToList();
            if (sprites.Count == 0) return;

            var back = new GameObject("06_Props_Fondo").transform;
            back.SetParent(parent, false);
            var front = new GameObject("07_Props_Frente").transform;
            front.SetParent(parent, false);

            var rng = new System.Random(def.era.GetHashCode());
            float x = 5f;
            int i = 0;
            while (x < def.length - 4f)
            {
                bool isFront = rng.NextDouble() < 0.25;
                var sprite = sprites[rng.Next(sprites.Count)];
                float scale = isFront ? 1.25f : (float)(0.8 + rng.NextDouble() * 0.2);
                float h = sprite.rect.height / PPU * scale;

                var go = new GameObject($"Prop_{i:00}", typeof(SpriteRenderer));
                go.transform.SetParent(isFront ? front : back, false);
                go.transform.position = new Vector3(x, h * 0.5f - 0.35f, 0f);
                go.transform.localScale = Vector3.one * scale;

                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = isFront ? 60 : -5;
                sr.color = isFront ? new Color(0.28f, 0.28f, 0.42f) : new Color(0.75f, 0.77f, 0.92f);
                if (rng.NextDouble() < 0.4) sr.flipX = true;

                x += (float)((isFront ? 10.0 : 6.0) + rng.NextDouble() * 6.0);
                i++;
            }

            foreach (var (root, factor) in new[] { (back, 0.05f), (front, -0.12f) })
            {
                var par = root.gameObject.AddComponent<ParallaxLayer>();
                var so = new SerializedObject(par);
                so.FindProperty("factorX").floatValue = factor;
                so.FindProperty("factorY").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static GameObject BuildEkkar(LevelDef def)
        {
            var root = new GameObject("Ekkar");
            root.transform.position = new Vector3(def.arena ? 8f : 6f, 0.2f, 0f);

            var body = new GameObject("Sprite", typeof(SpriteRenderer), typeof(Animator));
            body.transform.SetParent(root.transform, false);
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 0;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null) body.GetComponent<Animator>().runtimeAnimatorController = controller;

            var idle = AssetDatabase
                .LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Ekkar/Anim/ekkar_idle.png")
                .OfType<Sprite>().OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();
            if (idle.Length > 0) sr.sprite = idle[0];

            root.AddComponent<Rigidbody2D>().freezeRotation = true;
            var col = root.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.75f, 2.2f);
            col.offset = new Vector2(0f, 1.1f);
            col.direction = CapsuleDirection2D.Vertical;

            var check = new GameObject("SueloCheck").transform;
            check.SetParent(root.transform, false);
            check.localPosition = new Vector3(0f, 0.05f, 0f);

            var ctrl = root.AddComponent<EkkarController>();
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("groundCheck").objectReferenceValue = check;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var respawn = root.AddComponent<PlayerRespawn>();
            var rso = new SerializedObject(respawn);
            rso.FindProperty("levelStart").vector2Value = root.transform.position;
            rso.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Devuelve el velo de destello, que reutiliza el director del jefe.</summary>
        static SpriteRenderer BuildOverlays(LevelDef def, Transform camera)
        {
            var solid = Load($"{UiArt}/ui_solid.png");
            var vignette = Load($"{UiArt}/ui_vignette.png");
            var dot = Load($"{UiArt}/ui_dot.png");

            SpriteRenderer FullScreen(string name, Sprite sprite, Color color, int order)
            {
                var go = new GameObject(name, typeof(SpriteRenderer));
                go.transform.SetParent(camera, false);
                go.transform.localPosition = new Vector3(0f, 0f, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = color;
                sr.sortingOrder = order;
                if (sprite != null)
                {
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = new Vector2(ScreenHeight * 16f / 9f + 0.6f, ScreenHeight + 0.6f);
                }
                return sr;
            }

            FullScreen("Vineta", vignette, new Color(1f, 1f, 1f, 0.8f), 190);
            var flash = FullScreen("DestelloTemporal", solid, new Color(0.13f, 0.83f, 0.93f, 0f), 200);

            // particulas propias de la era, que se revelan poco a poco
            var p = def.particles;
            var go2 = new GameObject("ParticulasDeEra");
            go2.transform.SetParent(camera, false);
            go2.transform.localPosition = new Vector3(0f, 0f, 2f);

            var ef = go2.AddComponent<EmberFall>();
            var so = new SerializedObject(ef);
            so.FindProperty("moteSprite").objectReferenceValue = dot;
            so.FindProperty("count").intValue = p.count;
            so.FindProperty("revealSeconds").floatValue = p.reveal;
            so.FindProperty("emberRatio").floatValue = p.emberRatio;
            so.FindProperty("fallSpeed").vector2Value = p.fall;
            so.FindProperty("sizeRange").vector2Value = p.size;
            so.FindProperty("swayAmount").floatValue = p.sway;
            so.FindProperty("emberHot").colorValue = p.hot;
            so.FindProperty("emberCool").colorValue = p.cool;
            so.FindProperty("ash").colorValue = p.dust;
            so.FindProperty("chrono").colorValue = p.accent;
            so.ApplyModifiedPropertiesWithoutUndo();

            return flash;
        }

        static void BuildCheckpoints(LevelDef def, Transform parent)
        {
            var root = new GameObject("08_Guardado").transform;
            root.SetParent(parent, false);

            var gear = Load($"{UiArt}/ui_gear_a.png");
            var glow = Load($"{UiArt}/ui_glow.png");

            var xs = def.arena
                ? new[] { 6f, def.length * 0.5f }
                : new[] { 4f, def.length * 0.27f, def.length * 0.5f, def.length * 0.68f, def.length * 0.85f };

            for (int i = 0; i < xs.Length; i++)
            {
                var go = new GameObject($"Hoguera_{i:00}", typeof(BoxCollider2D), typeof(Checkpoint));
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(xs[i], 1.4f, 0f);

                var box = go.GetComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.8f, 3.2f);

                var glowGo = new GameObject("Resplandor", typeof(SpriteRenderer));
                glowGo.transform.SetParent(go.transform, false);
                var glowSr = glowGo.GetComponent<SpriteRenderer>();
                glowSr.sprite = glow;
                glowSr.sortingOrder = 20;
                glowGo.transform.localScale = Vector3.one * 2.2f;

                var symGo = new GameObject("Engranaje", typeof(SpriteRenderer));
                symGo.transform.SetParent(go.transform, false);
                var symSr = symGo.GetComponent<SpriteRenderer>();
                symSr.sprite = gear;
                symSr.sortingOrder = 21;
                symGo.transform.localScale = Vector3.one * 1.1f;
                symGo.AddComponent<UISpin>();

                var cp = go.GetComponent<Checkpoint>();
                var so = new SerializedObject(cp);
                so.FindProperty("id").stringValue = $"{def.era.ToLower()}_{i:00}";
                so.FindProperty("glow").objectReferenceValue = glowSr;
                so.FindProperty("symbol").objectReferenceValue = symSr;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildSystems(LevelDef def)
        {
            var host = new GameObject("_Sistemas");
            host.AddComponent<AudioSource>();
            var music = host.AddComponent<LevelMusic>();
            var so = new SerializedObject(music);
            so.FindProperty("era").enumValueIndex = (int)def.music;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildSectionSwap(Transform camera, GameObject before, GameObject after, float switchX)
        {
            var go = new GameObject("CambioDeTramo");
            var swap = go.AddComponent<SectionSwap>();
            var so = new SerializedObject(swap);
            so.FindProperty("target").objectReferenceValue = camera;
            so.FindProperty("before").objectReferenceValue = before;
            so.FindProperty("after").objectReferenceValue = after;
            so.FindProperty("switchX").floatValue = switchX;
            so.FindProperty("blendWidth").floatValue = 16f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Telones de las tres eras para la fase 2 del jefe final, mas el
        /// director que los va alternando al azar.
        /// </summary>
        static void BuildBossDirector(Transform fondo, SpriteRenderer flash)
        {
            var arena = new GameObject("00_Telon_Vacio").transform;
            arena.SetParent(fondo, false);
            // el vacio ya esta montado como capa normal; aqui se agrupan solo
            // los ecos de las otras eras, que empiezan invisibles
            foreach (Transform child in fondo)
                if (child.name.Contains("Vacio") || child.name.Contains("Trono"))
                    child.SetParent(arena, true);

            var eras = new (string era, string sky, string structure, string label)[]
            {
                ("Medieval",   "bg_cielo.png",    "bg_castillo.png",   "Eco_Medieval"),
                ("Industrial", "bg_00_cielo.png", "bg_02_fabrica.png", "Eco_Industrial"),
                ("Futuro",     "bg_00_cielo.png", "bg_02_servidor.png","Eco_Futuro"),
            };

            var groups = new List<GameObject>();
            foreach (var e in eras)
            {
                var root = new GameObject(e.label).transform;
                root.SetParent(fondo, false);

                AddBackdrop(root, $"{BG}/{e.era}/{e.sky}", -95, CameraY);
                AddBackdrop(root, $"{BG}/{e.era}/{e.structure}", -82, 2.2f);

                var par = root.gameObject.AddComponent<ParallaxLayer>();
                var pso = new SerializedObject(par);
                pso.FindProperty("factorX").floatValue = 0.85f;
                pso.FindProperty("factorY").floatValue = 0.85f;
                pso.ApplyModifiedPropertiesWithoutUndo();

                groups.Add(root.gameObject);
            }

            var dirGo = new GameObject("DirectorDelJefe");
            var dir = dirGo.AddComponent<BossPhaseDirector>();
            var so = new SerializedObject(dir);
            so.FindProperty("arenaBackdrop").objectReferenceValue = arena.gameObject;
            var arr = so.FindProperty("eraBackdrops");
            arr.arraySize = groups.Count;
            for (int i = 0; i < groups.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = groups[i];
            so.FindProperty("flashOverlay").objectReferenceValue = flash;
            so.FindProperty("phase").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddBackdrop(Transform parent, string spritePath, int order, float y)
        {
            var sprite = Load(spritePath);
            if (sprite == null) { Debug.LogWarning($"[Ekkar] Falta {spritePath}"); return; }

            float w = sprite.rect.width / PPU;
            for (int i = -1; i <= 1; i++)
            {
                var go = new GameObject($"{Path.GetFileNameWithoutExtension(spritePath)}_{i + 1}", typeof(SpriteRenderer));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(i * w, y, 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
                sr.color = new Color(1f, 1f, 1f, 0f);   // arrancan invisibles
            }
        }

        static void RegisterInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
