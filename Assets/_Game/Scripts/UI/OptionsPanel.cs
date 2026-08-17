using System.Collections;
using System.Collections.Generic;
using Ekkar.Audio;
using Ekkar.Core;
using TMPro;
using UnityEngine;

namespace Ekkar.UI
{
    /// <summary>
    /// Logica del panel de OPCIONES: musica, efectos, resolucion, modo de
    /// pantalla, vsync y particulas. El audio y las particulas se aplican al
    /// instante; la resolucion espera a GUARDAR. CANCELAR restaura el estado
    /// que habia al abrir el panel.
    /// </summary>
    [AddComponentMenu("Ekkar/Options Panel")]
    public class OptionsPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] UIPanel panel;

        [Header("Controles")]
        [SerializeField] LabeledSlider musicSlider;
        [SerializeField] LabeledSlider sfxSlider;
        [SerializeField] OptionSelector resolutionSelector;
        [SerializeField] OptionSelector screenModeSelector;
        [SerializeField] PixelToggle vsyncToggle;
        [SerializeField] PixelToggle particlesToggle;

        [Header("Aviso")]
        [SerializeField] CanvasGroup savedNotice;

        static readonly string[] k_ScreenModes =
        {
            "PANTALLA COMPLETA",
            "SIN BORDES",
            "VENTANA"
        };

        List<Vector2Int> _resolutions;

        // instantanea para CANCELAR
        float _snapMusic, _snapSfx;
        int _snapW, _snapH;
        ScreenMode _snapMode;
        bool _snapVsync, _snapParticles;

        Coroutine _noticeCo;

        void Awake()
        {
            if (panel == null) panel = GetComponent<UIPanel>();
            _resolutions = GameSettings.GetAvailableResolutions();

            if (musicSlider != null) musicSlider.ValueChanged += OnMusicChanged;
            if (sfxSlider != null) sfxSlider.ValueChanged += OnSfxChanged;
            if (resolutionSelector != null) resolutionSelector.IndexChanged += OnResolutionChanged;
            if (screenModeSelector != null) screenModeSelector.IndexChanged += OnScreenModeChanged;
            if (vsyncToggle != null) vsyncToggle.ValueChanged += OnVsyncChanged;
            if (particlesToggle != null) particlesToggle.ValueChanged += OnParticlesChanged;

            if (panel != null) panel.onOpened.AddListener(RefreshFromSettings);

            if (savedNotice != null) savedNotice.alpha = 0f;
        }

        void OnDestroy()
        {
            if (musicSlider != null) musicSlider.ValueChanged -= OnMusicChanged;
            if (sfxSlider != null) sfxSlider.ValueChanged -= OnSfxChanged;
            if (resolutionSelector != null) resolutionSelector.IndexChanged -= OnResolutionChanged;
            if (screenModeSelector != null) screenModeSelector.IndexChanged -= OnScreenModeChanged;
            if (vsyncToggle != null) vsyncToggle.ValueChanged -= OnVsyncChanged;
            if (particlesToggle != null) particlesToggle.ValueChanged -= OnParticlesChanged;
        }

        // ------------------------------------------------------------ carga

        public void RefreshFromSettings()
        {
            if (!GameSettings.Loaded) GameSettings.Load();
            TakeSnapshot();

            if (_resolutions == null || _resolutions.Count == 0)
                _resolutions = GameSettings.GetAvailableResolutions();

            var labels = new string[_resolutions.Count];
            for (int i = 0; i < _resolutions.Count; i++)
                labels[i] = $"{_resolutions[i].x} x {_resolutions[i].y}";

            if (resolutionSelector != null)
                resolutionSelector.SetOptions(labels, GameSettings.IndexOfResolution(_resolutions, GameSettings.ResWidth, GameSettings.ResHeight));

            if (screenModeSelector != null)
                screenModeSelector.SetOptions(k_ScreenModes, (int)GameSettings.Mode);

            if (musicSlider != null) musicSlider.SetValue(GameSettings.MusicVolume, false);
            if (sfxSlider != null) sfxSlider.SetValue(GameSettings.SfxVolume, false);
            if (vsyncToggle != null) vsyncToggle.SetValue(GameSettings.VSync, false, false);
            if (particlesToggle != null) particlesToggle.SetValue(GameSettings.Particles, false, false);

            if (savedNotice != null) savedNotice.alpha = 0f;
        }

        void TakeSnapshot()
        {
            _snapMusic = GameSettings.MusicVolume;
            _snapSfx = GameSettings.SfxVolume;
            _snapW = GameSettings.ResWidth;
            _snapH = GameSettings.ResHeight;
            _snapMode = GameSettings.Mode;
            _snapVsync = GameSettings.VSync;
            _snapParticles = GameSettings.Particles;
        }

        // ---------------------------------------------------------- cambios

        void OnMusicChanged(float v)
        {
            GameSettings.MusicVolume = v;
            GameSettings.ApplyAudio();
        }

        void OnSfxChanged(float v)
        {
            GameSettings.SfxVolume = v;
            GameSettings.ApplyAudio();
        }

        void OnResolutionChanged(int i)
        {
            if (_resolutions == null || i < 0 || i >= _resolutions.Count) return;
            GameSettings.ResWidth = _resolutions[i].x;
            GameSettings.ResHeight = _resolutions[i].y;
        }

        void OnScreenModeChanged(int i)
        {
            GameSettings.Mode = (ScreenMode)Mathf.Clamp(i, 0, 2);
        }

        void OnVsyncChanged(bool v)
        {
            GameSettings.VSync = v;
        }

        void OnParticlesChanged(bool v)
        {
            GameSettings.Particles = v;
            GameSettings.ApplyFx();
        }

        // ------------------------------------------------------- acciones

        /// <summary>Guarda en disco, aplica graficos y cierra.</summary>
        public void Guardar()
        {
            GameSettings.Save();
            GameSettings.ApplyGraphics();
            AudioManager.Confirm();
            Tween.Restart(this, ref _noticeCo, SaveNoticeRoutine());
        }

        /// <summary>Descarta los cambios hechos desde que se abrio el panel.</summary>
        public void Cancelar()
        {
            GameSettings.MusicVolume = _snapMusic;
            GameSettings.SfxVolume = _snapSfx;
            GameSettings.ResWidth = _snapW;
            GameSettings.ResHeight = _snapH;
            GameSettings.Mode = _snapMode;
            GameSettings.VSync = _snapVsync;
            GameSettings.Particles = _snapParticles;
            GameSettings.ApplyAudio();
            GameSettings.ApplyFx();

            AudioManager.Back();
            if (panel != null) panel.Close();
        }

        /// <summary>Vuelve a los valores de fabrica (sin guardar todavia).</summary>
        public void Restablecer()
        {
            GameSettings.ResetToDefaults();
            GameSettings.ApplyAudio();
            GameSettings.ApplyFx();

            if (musicSlider != null) musicSlider.SetValue(GameSettings.MusicVolume, false);
            if (sfxSlider != null) sfxSlider.SetValue(GameSettings.SfxVolume, false);
            if (resolutionSelector != null)
                resolutionSelector.SetIndex(GameSettings.IndexOfResolution(_resolutions, GameSettings.ResWidth, GameSettings.ResHeight), false);
            if (screenModeSelector != null) screenModeSelector.SetIndex((int)GameSettings.Mode, false);
            if (vsyncToggle != null) vsyncToggle.SetValue(GameSettings.VSync, false);
            if (particlesToggle != null) particlesToggle.SetValue(GameSettings.Particles, false);

            AudioManager.Toggle();
        }

        IEnumerator SaveNoticeRoutine()
        {
            if (savedNotice != null)
                yield return Tween.Fade(savedNotice, 1f, 0.18f, Ease.OutQuad);

            yield return Tween.Wait(0.55f);

            if (savedNotice != null)
                yield return Tween.Fade(savedNotice, 0f, 0.2f, Ease.InQuad);

            if (panel != null) panel.Close();
        }
    }
}
