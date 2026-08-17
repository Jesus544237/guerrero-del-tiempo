using UnityEngine;

namespace Ekkar.Core
{
    /// <summary>
    /// Paleta cromatica unica del juego. Todo el menu (y el arte generado por
    /// el editor) toma los colores de aqui, asi que cambiar un valor aqui
    /// repinta el menu entero.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Void        = FromHex("0A0515");
        public static readonly Color PurpleDeep  = FromHex("1A0A2E");
        public static readonly Color PurpleMid   = FromHex("2D1B69");
        public static readonly Color PurpleLight = FromHex("7C3AED");
        public static readonly Color Cyan        = FromHex("06B6D4");
        public static readonly Color CyanBright  = FromHex("22D3EE");
        public static readonly Color Gold        = FromHex("F59E0B");
        public static readonly Color GoldLight   = FromHex("FBBF24");
        public static readonly Color RedCape     = FromHex("DC2626");
        public static readonly Color Armor       = FromHex("E2E8F0");
        public static readonly Color TextDim     = FromHex("94A3B8");

        public static Color FromHex(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length < 6) return Color.magenta;

            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            float a = hex.Length >= 8 ? System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : alpha;
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Mezcla hacia negro sin tocar el alfa.</summary>
        public static Color Darken(this Color c, float amount)
        {
            return new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);
        }

        /// <summary>Mezcla hacia blanco sin tocar el alfa.</summary>
        public static Color Lighten(this Color c, float amount)
        {
            return new Color(
                Mathf.Lerp(c.r, 1f, amount),
                Mathf.Lerp(c.g, 1f, amount),
                Mathf.Lerp(c.b, 1f, amount), c.a);
        }
    }
}
