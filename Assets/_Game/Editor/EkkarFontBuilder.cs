using System.IO;
using System.Text;
using Ekkar.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Crea el TMP_FontAsset de la fuente pixel (Perfect DOS VGA 437) y tres
    /// materiales derivados: titulo (dorado con resplandor), acento cian y
    /// texto general. Todos son assets reales del proyecto, editables desde el
    /// Inspector.
    /// </summary>
    public static class EkkarFontBuilder
    {
        public const string FontFolder = "Assets/_Game/Art/Fonts";
        public const string TtfPath = FontFolder + "/PerfectDOSVGA437.ttf";
        public const string FontAssetPath = FontFolder + "/Ekkar_Pixel_SDF.asset";

        public const string MatTitle = FontFolder + "/M_Texto_Titulo.mat";
        public const string MatAccent = FontFolder + "/M_Texto_Cian.mat";
        public const string MatBody = FontFolder + "/M_Texto_Base.mat";
        public const string MatSans = FontFolder + "/M_Texto_Prosa.mat";

        /// <summary>Fuente de respaldo con Unicode completo (viene con TextMeshPro).</summary>
        public const string SansFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        /// <summary>
        /// Perfect DOS VGA 437 mapea los codigos 0x00-0xFF directamente a las
        /// posiciones de la pagina de codigos 437, asi que sus "acentos" son en
        /// realidad caracteres de dibujo de cajas. Por eso el atlas se limita al
        /// ASCII imprimible y todo lo demas (tildes, enes, signos de apertura)
        /// se resuelve con la fuente de respaldo.
        /// </summary>
        static string BuildCharset()
        {
            var sb = new StringBuilder();
            for (int c = 0x20; c <= 0x7E; c++) sb.Append((char)c);
            return sb.ToString();
        }

        public static TMP_FontAsset LoadSansFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SansFontPath);
        }

        public static TMP_FontAsset BuildFontAsset(bool force = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null && !force) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (font == null)
            {
                Debug.LogError($"[Ekkar] No encuentro la fuente en {TtfPath}.");
                return null;
            }

            if (existing != null) AssetDatabase.DeleteAsset(FontAssetPath);

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[Ekkar] TMP no pudo crear el font asset.");
                return null;
            }

            Directory.CreateDirectory(FontFolder);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // el atlas y el material deben quedar como sub-assets
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                fontAsset.atlasTextures[0].name = "Ekkar_Pixel_SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Ekkar_Pixel_SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            fontAsset.TryAddCharacters(BuildCharset());

            // Congela el atlas en el ASCII generado y delega el resto (acentos,
            // enes, ¿ ¡) en Liberation Sans, que si es Unicode correcto.
            var sans = LoadSansFont();
            if (sans != null)
            {
                fontAsset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { sans };
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            }
            else
            {
                Debug.LogWarning("[Ekkar] No encuentro Liberation Sans SDF; la fuente pixel se queda en modo dinamico.");
            }

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
                EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Ekkar] Fuente pixel creada en {FontAssetPath}");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        public static void BuildMaterials(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null) return;

            CreatePreset(fontAsset, MatTitle, mat =>
            {
                Set(mat, "_FaceDilate", 0.08f);
                Outline(mat, Palette.Void.WithAlpha(1f), 0.16f);
                Underlay(mat, new Color(0f, 0f, 0f, 0.85f), 0.9f, -0.9f, 0.25f, 0.12f);
                Glow(mat, Palette.Gold.WithAlpha(1f), 0.35f, 0.22f);
            });

            CreatePreset(fontAsset, MatAccent, mat =>
            {
                Set(mat, "_FaceDilate", 0.02f);
                Outline(mat, Palette.Void.WithAlpha(0.95f), 0.10f);
                Underlay(mat, new Color(0f, 0f, 0f, 0.7f), 0.6f, -0.6f, 0.1f, 0.1f);
                Glow(mat, Palette.CyanBright.WithAlpha(1f), 0.28f, 0.18f);
            });

            CreatePreset(fontAsset, MatBody, mat =>
            {
                Set(mat, "_FaceDilate", 0.0f);
                Underlay(mat, new Color(0f, 0f, 0f, 0.75f), 0.55f, -0.55f, 0.08f, 0.1f);
            });

            // material de prosa sobre Liberation Sans (textos con tildes)
            var sans = LoadSansFont();
            if (sans != null && sans.material != null)
            {
                CreatePreset(sans, MatSans, mat =>
                {
                    Underlay(mat, new Color(0f, 0f, 0f, 0.8f), 0.5f, -0.5f, 0.1f, 0.12f);
                });
            }

            AssetDatabase.SaveAssets();
        }

        static void CreatePreset(TMP_FontAsset fontAsset, string path, System.Action<Material> configure)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(fontAsset.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = fontAsset.material.shader;
                mat.CopyPropertiesFromMaterial(fontAsset.material);
            }

            configure(mat);
            EditorUtility.SetDirty(mat);
        }

        static void Set(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        static void Outline(Material m, Color color, float width)
        {
            if (!m.HasProperty("_OutlineWidth")) return;
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", color);
            m.SetFloat("_OutlineWidth", width);
        }

        static void Underlay(Material m, Color color, float offsetX, float offsetY, float dilate, float softness)
        {
            if (!m.HasProperty("_UnderlayColor")) return;
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", color);
            m.SetFloat("_UnderlayOffsetX", offsetX);
            m.SetFloat("_UnderlayOffsetY", offsetY);
            m.SetFloat("_UnderlayDilate", dilate);
            m.SetFloat("_UnderlaySoftness", softness);
        }

        static void Glow(Material m, Color color, float power, float outer)
        {
            if (!m.HasProperty("_GlowPower")) return;
            m.EnableKeyword("GLOW_ON");
            m.SetColor("_GlowColor", color);
            m.SetFloat("_GlowPower", power);
            m.SetFloat("_GlowOuter", outer);
            if (m.HasProperty("_GlowInner")) m.SetFloat("_GlowInner", 0.05f);
        }
    }
}
