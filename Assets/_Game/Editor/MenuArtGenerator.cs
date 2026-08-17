using System.Collections.Generic;
using Ekkar.Core;
using UnityEditor;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Genera por codigo todo el "kit" de interfaz pixel del menu: marcos,
    /// rellenos, engranajes, esfera de reloj, flechas, deslizadores, vineta,
    /// lineas de barrido... Se puede volver a ejecutar en cualquier momento
    /// para repintar el menu si se cambia la paleta.
    /// </summary>
    public static class MenuArtGenerator
    {
        public const string Folder = "Assets/_Game/Art/UI/Generated";

        static Color A(float a) => new Color(1f, 1f, 1f, a);

        public static Dictionary<string, Sprite> GenerateAll()
        {
            var made = new Dictionary<string, Sprite>();

            made["solid"]        = Solid();
            made["frame"]        = ButtonFrame();
            made["fill"]         = ButtonFill();
            made["brackets"]     = CornerBrackets();
            made["panel_frame"]  = PanelFrame();
            made["panel_fill"]   = PanelFill();
            made["grad_v"]       = GradientV();
            made["grad_h"]       = GradientH();
            made["screen_tint"]  = ScreenTint();
            made["dot"]          = Dot();
            made["spark"]        = Spark();
            made["gear_a"]       = Gear("ui_gear_a", 64, 10, 31, 25, 16, 10, 4);
            made["gear_b"]       = Gear("ui_gear_b", 48, 8, 23, 18, 11, 7, 3);
            made["gear_c"]       = Gear("ui_gear_c", 32, 6, 15, 12, 7, 5, 2);
            made["clock_ring"]   = ClockRing();
            made["clock_hand"]   = ClockHand("ui_clock_hand_long", 16, 200, 88, 3);
            made["clock_hand_s"] = ClockHand("ui_clock_hand_short", 16, 200, 60, 5);
            made["diamond"]      = Diamond();
            made["arrow_r"]      = Triangle("ui_arrow_right", 14, false);
            made["arrow_l"]      = Triangle("ui_arrow_left", 14, true);
            made["chevron_r"]    = Chevron("ui_chevron_right", false);
            made["chevron_l"]    = Chevron("ui_chevron_left", true);
            made["scanlines"]    = Scanlines();
            made["vignette"]     = Vignette();
            made["glow"]         = RadialGlow();
            made["slider_track"] = SliderTrack();
            made["slider_fill"]  = SliderFill();
            made["slider_knob"]  = SliderKnob();
            made["toggle_track"] = ToggleTrack();
            made["toggle_knob"]  = ToggleKnob();
            made["keycap"]       = KeyCap();
            made["scroll_bar"]   = ScrollHandle();

            AssetDatabase.Refresh();
            return made;
        }

        // ------------------------------------------------------------ basicos

        static Sprite Solid()
        {
            var t = PixelTex.New(4, 4);
            PixelTex.Fill(t, Color.white);
            return PixelTex.Save(t, Folder, "ui_solid");
        }

        static Sprite Dot()
        {
            var t = PixelTex.New(4, 4);
            PixelTex.Fill(t, Color.white);
            PixelTex.Px(t, 0, 0, A(0f)); PixelTex.Px(t, 3, 0, A(0f));
            PixelTex.Px(t, 0, 3, A(0f)); PixelTex.Px(t, 3, 3, A(0f));
            return PixelTex.Save(t, Folder, "ui_dot");
        }

        static Sprite Spark()
        {
            var t = PixelTex.New(7, 7);
            PixelTex.FillRect(t, 3, 0, 1, 7, A(0.75f));
            PixelTex.FillRect(t, 0, 3, 7, 1, A(0.75f));
            PixelTex.FillRect(t, 2, 2, 3, 3, A(1f));
            PixelTex.Px(t, 2, 2, A(0.5f)); PixelTex.Px(t, 4, 2, A(0.5f));
            PixelTex.Px(t, 2, 4, A(0.5f)); PixelTex.Px(t, 4, 4, A(0.5f));
            return PixelTex.Save(t, Folder, "ui_spark");
        }

        // ---------------------------------------------------- marcos 9-slice

        /// <summary>Marco fino del boton: contorno exterior + linea interior tenue.</summary>
        static Sprite ButtonFrame()
        {
            const int S = 24;
            var t = PixelTex.New(S, S);
            PixelTex.RectOutline(t, 0, 0, S, S, 1, A(1f));
            PixelTex.RectOutline(t, 2, 2, S - 4, S - 4, 1, A(0.22f));
            // muescas de esquina
            foreach (var (cx, cy) in new[] { (0, 0), (S - 4, 0), (0, S - 4), (S - 4, S - 4) })
                PixelTex.FillRect(t, cx, cy, 4, 4, A(0f));
            PixelTex.RectOutline(t, 0, 0, S, S, 1, A(1f));
            return PixelTex.Save(t, Folder, "ui_frame", new Vector4(8, 8, 8, 8));
        }

        /// <summary>Relleno del boton (se tine desde el componente Image).</summary>
        static Sprite ButtonFill()
        {
            const int S = 24;
            var t = PixelTex.New(S, S);
            PixelTex.FillRect(t, 1, 1, S - 2, S - 2, A(1f));
            PixelTex.RectOutline(t, 1, 1, S - 2, S - 2, 1, A(0.72f));
            return PixelTex.Save(t, Folder, "ui_fill", new Vector4(8, 8, 8, 8));
        }

        /// <summary>Cuatro escuadras de esquina (para destacar el boton JUGAR).</summary>
        static Sprite CornerBrackets()
        {
            const int S = 32;
            const int arm = 9, th = 2;
            var t = PixelTex.New(S, S);

            PixelTex.FillRect(t, 0, 0, arm, th, A(1f));
            PixelTex.FillRect(t, 0, 0, th, arm, A(1f));

            PixelTex.FillRect(t, S - arm, 0, arm, th, A(1f));
            PixelTex.FillRect(t, S - th, 0, th, arm, A(1f));

            PixelTex.FillRect(t, 0, S - th, arm, th, A(1f));
            PixelTex.FillRect(t, 0, S - arm, th, arm, A(1f));

            PixelTex.FillRect(t, S - arm, S - th, arm, th, A(1f));
            PixelTex.FillRect(t, S - th, S - arm, th, arm, A(1f));

            return PixelTex.Save(t, Folder, "ui_brackets", new Vector4(12, 12, 12, 12));
        }

        /// <summary>Marco doble ornamentado de las ventanas modales.</summary>
        static Sprite PanelFrame()
        {
            const int S = 40;
            var t = PixelTex.New(S, S);
            PixelTex.RectOutline(t, 0, 0, S, S, 2, A(1f));
            PixelTex.RectOutline(t, 4, 4, S - 8, S - 8, 1, A(0.45f));
            PixelTex.RectOutline(t, 6, 6, S - 12, S - 12, 1, A(0.16f));

            // remaches en las esquinas
            foreach (var (x, y) in new[] { (4, 4), (S - 7, 4), (4, S - 7), (S - 7, S - 7) })
                PixelTex.FillRect(t, x, y, 3, 3, A(1f));

            return PixelTex.Save(t, Folder, "ui_panel_frame", new Vector4(14, 14, 14, 14));
        }

        static Sprite PanelFill()
        {
            const int S = 40;
            var t = PixelTex.New(S, S);
            PixelTex.FillRect(t, 1, 1, S - 2, S - 2, A(1f));
            return PixelTex.Save(t, Folder, "ui_panel_fill", new Vector4(14, 14, 14, 14));
        }

        // --------------------------------------------------------- degradados

        static Sprite GradientV()
        {
            const int W = 4, H = 64;
            var t = PixelTex.New(W, H);
            for (int y = 0; y < H; y++)
            {
                float a = 1f - (y / (float)(H - 1));
                PixelTex.FillRect(t, 0, y, W, 1, A(a * a));
            }
            return PixelTex.Save(t, Folder, "ui_gradient_v");
        }

        static Sprite GradientH()
        {
            const int W = 64, H = 4;
            var t = PixelTex.New(W, H);
            for (int x = 0; x < W; x++)
            {
                float k = x / (float)(W - 1);
                float a = Mathf.Sin(k * Mathf.PI);
                PixelTex.FillRect(t, x, 0, 1, H, A(a * a));
            }
            return PixelTex.Save(t, Folder, "ui_gradient_h");
        }

        /// <summary>Velo de color a pantalla completa que unifica la ilustracion.</summary>
        static Sprite ScreenTint()
        {
            const int W = 8, H = 256;
            var t = PixelTex.New(W, H);
            for (int y = 0; y < H; y++)
            {
                float k = 1f - y / (float)(H - 1);   // 0 arriba, 1 abajo
                Color c;
                if (k < 0.35f) c = Color.Lerp(Palette.Void, Palette.PurpleDeep, k / 0.35f);
                else if (k < 0.7f) c = Color.Lerp(Palette.PurpleDeep, Palette.PurpleMid, (k - 0.35f) / 0.35f);
                else c = Color.Lerp(Palette.PurpleMid, Palette.Void, (k - 0.7f) / 0.3f);

                c.a = Mathf.Lerp(0.62f, 0.30f, Mathf.Sin(k * Mathf.PI));
                PixelTex.FillRect(t, 0, y, W, 1, c);
            }
            return PixelTex.Save(t, Folder, "ui_screen_tint");
        }

        // ---------------------------------------------------------- adornos

        static Sprite Gear(string name, int size, int teeth, float rTooth, float rBody,
                           float rInner, float rHub, float rHole)
        {
            var t = PixelTex.New(size, size);
            float c = size * 0.5f - 0.5f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float phase = Mathf.Repeat(ang / (Mathf.PI * 2f) * teeth, 1f);

                    bool solid = false;
                    float outer = phase < 0.5f ? rTooth : rBody;

                    if (r <= outer && r >= rInner) solid = true;             // corona + dientes
                    if (r <= rHub && r >= rHole) solid = true;               // cubo central

                    if (!solid && r < rInner && r > rHub)                    // radios
                    {
                        float spoke = Mathf.Repeat(ang / (Mathf.PI * 2f) * 4f, 1f);
                        if (spoke < 0.13f || spoke > 0.87f) solid = true;
                    }

                    if (solid) PixelTex.Px(t, x, y, Color.white);
                }

            PixelTex.InnerShade(t, 0.62f);
            return PixelTex.Save(t, Folder, name);
        }

        static Sprite ClockRing()
        {
            const int S = 256;
            var t = PixelTex.New(S, S);
            float c = S * 0.5f - 0.5f;

            PixelTex.Ring(t, c, c, 126f, 123f, A(0.95f));
            PixelTex.Ring(t, c, c, 112f, 111f, A(0.35f));
            PixelTex.Ring(t, c, c, 64f, 63f, A(0.18f));

            for (int i = 0; i < 60; i++)
            {
                float ang = i * Mathf.PI * 2f / 60f - Mathf.PI * 0.5f;
                bool major = i % 5 == 0;
                float r0 = major ? 100f : 114f;
                float r1 = 121f;
                float a = major ? 0.95f : 0.4f;
                int th = major ? 3 : 1;

                PixelTex.Line(t,
                    Mathf.RoundToInt(c + Mathf.Cos(ang) * r0), Mathf.RoundToInt(c + Mathf.Sin(ang) * r0),
                    Mathf.RoundToInt(c + Mathf.Cos(ang) * r1), Mathf.RoundToInt(c + Mathf.Sin(ang) * r1),
                    A(a), th);
            }

            return PixelTex.Save(t, Folder, "ui_clock_ring");
        }

        /// <summary>
        /// Aguja dibujada desde el centro hacia arriba, de modo que el pivote
        /// por defecto (centro del sprite) sea el eje del reloj.
        /// </summary>
        static Sprite ClockHand(string name, int w, int h, int length, int thickness)
        {
            var t = PixelTex.New(w, h);
            int cx = w / 2;
            int cy = h / 2;

            for (int i = 0; i < length; i++)
            {
                float k = i / (float)length;
                int th = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(thickness, 1f, k)));
                PixelTex.FillRect(t, cx - th / 2, cy + i, th, 1, A(1f));
            }
            PixelTex.FillRect(t, cx - thickness / 2 - 1, cy - 6, thickness + 2, 8, A(1f));

            PixelTex.InnerShade(t, 0.7f);
            return PixelTex.Save(t, Folder, name);
        }

        static Sprite Diamond()
        {
            const int S = 16;
            var t = PixelTex.New(S, S);
            float c = S * 0.5f - 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    if (Mathf.Abs(x - c) + Mathf.Abs(y - c) <= c) PixelTex.Px(t, x, y, Color.white);

            PixelTex.InnerShade(t, 0.55f);
            PixelTex.FillRect(t, 5, 9, 2, 2, A(1f));   // brillo
            return PixelTex.Save(t, Folder, "ui_diamond");
        }

        static Sprite Triangle(string name, int size, bool flip)
        {
            var t = PixelTex.New(size, size);
            for (int y = 0; y < size; y++)
            {
                float k = 1f - Mathf.Abs(y - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                int w = Mathf.Max(1, Mathf.RoundToInt(k * size * 0.6f));
                int x0 = flip ? size - w : 0;
                PixelTex.FillRect(t, x0, y, w, 1, Color.white);
            }
            PixelTex.InnerShade(t, 0.7f);
            return PixelTex.Save(t, Folder, name);
        }

        static Sprite Chevron(string name, bool flip)
        {
            const int W = 10, H = 16;
            var t = PixelTex.New(W, H);
            for (int i = 0; i < 8; i++)
            {
                int x = flip ? 7 - i : i;
                int y = H / 2 - i;
                PixelTex.FillRect(t, x, y, 2, 2, Color.white);
                PixelTex.FillRect(t, x, H - 2 - y, 2, 2, Color.white);
            }
            return PixelTex.Save(t, Folder, name);
        }

        // ---------------------------------------------------- capas de pantalla

        static Sprite Scanlines()
        {
            var t = PixelTex.New(4, 4);
            PixelTex.FillRect(t, 0, 0, 4, 1, new Color(0f, 0f, 0f, 0.30f));
            PixelTex.FillRect(t, 0, 1, 4, 1, new Color(0f, 0f, 0f, 0.10f));
            return PixelTex.Save(t, Folder, "ui_scanlines", default, 100, FilterMode.Point, TextureWrapMode.Repeat);
        }

        static Sprite Vignette()
        {
            const int S = 256;
            var t = PixelTex.New(S, S);
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float nx = (x - c) / c;
                    float ny = (y - c) / c;
                    float r = Mathf.Sqrt(nx * nx + ny * ny) / 1.41421f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, r)) * 0.92f;
                    t.SetPixel(x, y, new Color(0.02f, 0.01f, 0.05f, a));
                }
            return PixelTex.Save(t, Folder, "ui_vignette", default, 100, FilterMode.Bilinear);
        }

        static Sprite RadialGlow()
        {
            const int S = 128;
            var t = PixelTex.New(S, S);
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - r, 2.4f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            return PixelTex.Save(t, Folder, "ui_glow", default, 100, FilterMode.Bilinear);
        }

        // --------------------------------------------------------- controles

        static Sprite SliderTrack()
        {
            const int W = 16, H = 10;
            var t = PixelTex.New(W, H);
            PixelTex.FillRect(t, 1, 1, W - 2, H - 2, A(0.55f));
            PixelTex.RectOutline(t, 0, 0, W, H, 1, A(1f));
            return PixelTex.Save(t, Folder, "ui_slider_track", new Vector4(4, 4, 4, 4));
        }

        static Sprite SliderFill()
        {
            const int W = 16, H = 10;
            var t = PixelTex.New(W, H);
            PixelTex.FillRect(t, 0, 0, W, H, A(1f));
            PixelTex.FillRect(t, 0, H - 2, W, 1, A(0.55f));
            PixelTex.FillRect(t, 0, 1, W, 1, A(0.7f));
            return PixelTex.Save(t, Folder, "ui_slider_fill", new Vector4(4, 4, 4, 4));
        }

        static Sprite SliderKnob()
        {
            const int W = 14, H = 24;
            var t = PixelTex.New(W, H);
            PixelTex.FillRect(t, 1, 1, W - 2, H - 2, A(1f));
            PixelTex.RectOutline(t, 0, 0, W, H, 1, A(1f));
            PixelTex.FillRect(t, 3, H / 2 - 4, W - 6, 1, A(0.35f));
            PixelTex.FillRect(t, 3, H / 2, W - 6, 1, A(0.35f));
            PixelTex.FillRect(t, 3, H / 2 + 4, W - 6, 1, A(0.35f));
            PixelTex.Px(t, 0, 0, A(0f)); PixelTex.Px(t, W - 1, 0, A(0f));
            PixelTex.Px(t, 0, H - 1, A(0f)); PixelTex.Px(t, W - 1, H - 1, A(0f));
            return PixelTex.Save(t, Folder, "ui_slider_knob");
        }

        static Sprite ToggleTrack()
        {
            const int W = 28, H = 16;
            var t = PixelTex.New(W, H);
            PixelTex.FillRect(t, 1, 1, W - 2, H - 2, A(1f));
            PixelTex.FillRect(t, 2, 0, W - 4, 1, A(1f));
            PixelTex.FillRect(t, 2, H - 1, W - 4, 1, A(1f));
            PixelTex.FillRect(t, 0, 2, 1, H - 4, A(1f));
            PixelTex.FillRect(t, W - 1, 2, 1, H - 4, A(1f));
            return PixelTex.Save(t, Folder, "ui_toggle_track", new Vector4(7, 7, 7, 7));
        }

        static Sprite ToggleKnob()
        {
            const int S = 12;
            var t = PixelTex.New(S, S);
            PixelTex.FillRect(t, 0, 1, S, S - 2, A(1f));
            PixelTex.FillRect(t, 1, 0, S - 2, S, A(1f));
            PixelTex.FillRect(t, 2, S - 4, S - 4, 2, A(0.6f));
            return PixelTex.Save(t, Folder, "ui_toggle_knob");
        }

        static Sprite KeyCap()
        {
            const int S = 16;
            var t = PixelTex.New(S, S);
            PixelTex.FillRect(t, 1, 2, S - 2, S - 3, A(0.30f));
            PixelTex.RectOutline(t, 0, 1, S, S - 1, 1, A(0.85f));
            PixelTex.FillRect(t, 1, 0, S - 2, 1, A(0.5f));
            return PixelTex.Save(t, Folder, "ui_keycap", new Vector4(5, 5, 5, 5));
        }

        static Sprite ScrollHandle()
        {
            const int W = 8, H = 16;
            var t = PixelTex.New(W, H);
            PixelTex.FillRect(t, 1, 1, W - 2, H - 2, A(1f));
            PixelTex.RectOutline(t, 0, 0, W, H, 1, A(0.6f));
            return PixelTex.Save(t, Folder, "ui_scroll_handle", new Vector4(3, 3, 3, 3));
        }
    }
}
