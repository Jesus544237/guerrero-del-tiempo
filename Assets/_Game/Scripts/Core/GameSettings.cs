using System.Collections.Generic;
using UnityEngine;

namespace Ekkar.Core
{
    public enum ScreenMode
    {
        Fullscreen = 0,   // pantalla completa exclusiva
        Borderless = 1,   // ventana sin bordes a resolucion de escritorio
        Windowed   = 2    // ventana normal
    }

    /// <summary>
    /// Ajustes persistentes del juego (PlayerPrefs). Se cargan y aplican solos
    /// antes de que cargue la primera escena, asi que sirven tanto para el menu
    /// como para el resto del juego.
    /// </summary>
    public static class GameSettings
    {
        const string K_MUSIC_VOL = "ekkar.musicVolume";
        const string K_SFX_VOL   = "ekkar.sfxVolume";
        const string K_RES_W     = "ekkar.resWidth";
        const string K_RES_H     = "ekkar.resHeight";
        const string K_MODE      = "ekkar.screenMode";
        const string K_VSYNC     = "ekkar.vsync";
        const string K_FX        = "ekkar.particles";

        public static float      MusicVolume = 0.8f;
        public static float      SfxVolume   = 1.0f;
        public static int        ResWidth    = 1920;
        public static int        ResHeight   = 1080;
        public static ScreenMode Mode        = ScreenMode.Fullscreen;
        public static bool       VSync       = true;
        public static bool       Particles   = true;

        public static bool Loaded { get; private set; }

        /// <summary>Se dispara cuando cambia cualquier ajuste de audio.</summary>
        public static event System.Action AudioChanged;

        /// <summary>Se dispara cuando cambia el ajuste de particulas.</summary>
        public static event System.Action FxChanged;

        // ------------------------------------------------ resoluciones 16:9

        static readonly Vector2Int[] k_Preferred =
        {
            new Vector2Int(3840, 2160),
            new Vector2Int(2560, 1440),
            new Vector2Int(1920, 1080),
            new Vector2Int(1600,  900),
            new Vector2Int(1366,  768),
            new Vector2Int(1280,  720),
        };

        /// <summary>
        /// Resoluciones 16:9 que caben en el monitor actual. Siempre devuelve
        /// al menos una entrada.
        /// </summary>
        public static List<Vector2Int> GetAvailableResolutions()
        {
            var result = new List<Vector2Int>();

            int maxW = int.MaxValue, maxH = int.MaxValue;
#if !UNITY_EDITOR
            Resolution desktop = Screen.currentResolution;
            if (desktop.width > 0 && desktop.height > 0)
            {
                maxW = desktop.width;
                maxH = desktop.height;
            }
#endif
            foreach (var r in k_Preferred)
                if (r.x <= maxW && r.y <= maxH) result.Add(r);

            if (result.Count == 0) result.Add(new Vector2Int(1280, 720));
            return result;
        }

        public static int IndexOfResolution(List<Vector2Int> list, int w, int h)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].x == w && list[i].y == h) return i;

            // si la guardada ya no esta disponible, cae en la mas parecida
            int best = 0, bestDiff = int.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                int diff = Mathf.Abs(list[i].y - h);
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------ persistencia

        public static void Load()
        {
            MusicVolume = PlayerPrefs.GetFloat(K_MUSIC_VOL, 0.8f);
            SfxVolume   = PlayerPrefs.GetFloat(K_SFX_VOL,   1.0f);
            ResWidth    = PlayerPrefs.GetInt(K_RES_W, 1920);
            ResHeight   = PlayerPrefs.GetInt(K_RES_H, 1080);
            Mode        = (ScreenMode)PlayerPrefs.GetInt(K_MODE, (int)ScreenMode.Fullscreen);
            VSync       = PlayerPrefs.GetInt(K_VSYNC, 1) == 1;
            Particles   = PlayerPrefs.GetInt(K_FX, 1) == 1;
            Loaded      = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(K_MUSIC_VOL, MusicVolume);
            PlayerPrefs.SetFloat(K_SFX_VOL,   SfxVolume);
            PlayerPrefs.SetInt(K_RES_W, ResWidth);
            PlayerPrefs.SetInt(K_RES_H, ResHeight);
            PlayerPrefs.SetInt(K_MODE, (int)Mode);
            PlayerPrefs.SetInt(K_VSYNC, VSync ? 1 : 0);
            PlayerPrefs.SetInt(K_FX, Particles ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            MusicVolume = 0.8f;
            SfxVolume   = 1.0f;
            ResWidth    = 1920;
            ResHeight   = 1080;
            Mode        = ScreenMode.Fullscreen;
            VSync       = true;
            Particles   = true;
        }

        // --------------------------------------------------------- aplicar

        public static void ApplyAudio() => AudioChanged?.Invoke();
        public static void ApplyFx()    => FxChanged?.Invoke();

        public static void ApplyGraphics()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;

#if !UNITY_EDITOR
            FullScreenMode fsMode;
            switch (Mode)
            {
                case ScreenMode.Fullscreen: fsMode = FullScreenMode.ExclusiveFullScreen; break;
                case ScreenMode.Borderless: fsMode = FullScreenMode.FullScreenWindow;    break;
                default:                    fsMode = FullScreenMode.Windowed;            break;
            }

            int w = ResWidth;
            int h = ResHeight;
            if (Mode == ScreenMode.Borderless)
            {
                w = Screen.currentResolution.width;
                h = Screen.currentResolution.height;
            }

            if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != fsMode)
                Screen.SetResolution(w, h, fsMode);
#endif
        }

        public static void ApplyAll()
        {
            ApplyGraphics();
            ApplyAudio();
            ApplyFx();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Load();
            ApplyGraphics();
        }
    }
}
