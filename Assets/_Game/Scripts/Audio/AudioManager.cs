using Ekkar.Core;
using UnityEngine;

namespace Ekkar.Audio
{
    /// <summary>
    /// Punto unico de audio del menu: mantiene las dos fuentes (musica y
    /// efectos), aplica los volumenes guardados en <see cref="GameSettings"/>
    /// y expone atajos estaticos para que cualquier widget suene sin buscar
    /// referencias.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Fuentes")]
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;

        [Header("Musica")]
        [Tooltip("Si se asigna un clip aqui se usa en lugar del ambiente generado por codigo.")]
        [SerializeField] AudioClip musicOverride;
        [SerializeField] bool playMusicOnStart = true;
        [SerializeField, Range(0f, 1f)] float musicMix = 0.55f;

        [Header("Efectos (dejar vacio para generarlos por codigo)")]
        [SerializeField] AudioClip hoverOverride;
        [SerializeField] AudioClip clickOverride;
        [SerializeField] AudioClip backOverride;
        [SerializeField] AudioClip confirmOverride;
        [SerializeField] AudioClip toggleOverride;
        [SerializeField] AudioClip tickOverride;
        [SerializeField] AudioClip denyOverride;

        AudioClip _hover, _click, _back, _confirm, _toggle, _tick, _deny;
        float _lastTickTime;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (!GameSettings.Loaded) GameSettings.Load();

            EnsureSources();
            BuildClips();

            GameSettings.AudioChanged += ApplyVolumes;
            ApplyVolumes();
        }

        void Start()
        {
            if (playMusicOnStart) PlayMusic();
        }

        void OnDestroy()
        {
            GameSettings.AudioChanged -= ApplyVolumes;
            if (Instance == this) Instance = null;
        }

        void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }
            musicSource.spatialBlend = 0f;
            sfxSource.spatialBlend = 0f;
        }

        void BuildClips()
        {
            _hover   = hoverOverride   != null ? hoverOverride   : ProceduralAudio.Blip("ui_hover", 1180f, 1480f, 0.055f, Wave.Square, 0.22f, 9f);
            _click   = clickOverride   != null ? clickOverride   : ProceduralAudio.Blip("ui_click",  700f, 1400f, 0.085f, Wave.Square, 0.30f, 7f);
            _back    = backOverride    != null ? backOverride    : ProceduralAudio.Blip("ui_back",   700f,  330f, 0.110f, Wave.Square, 0.26f, 6f);
            _toggle  = toggleOverride  != null ? toggleOverride  : ProceduralAudio.Blip("ui_toggle", 990f, 1240f, 0.050f, Wave.Triangle, 0.28f, 10f);
            _tick    = tickOverride    != null ? tickOverride    : ProceduralAudio.Blip("ui_tick",  1650f, 1650f, 0.022f, Wave.Square, 0.12f, 16f);
            _deny    = denyOverride    != null ? denyOverride    : ProceduralAudio.Blip("ui_deny",   240f,  150f, 0.180f, Wave.Saw,    0.24f, 5f);
            _confirm = confirmOverride != null ? confirmOverride : ProceduralAudio.Arpeggio("ui_confirm",
                          new[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.055f, Wave.Square, 0.26f, 9f);

            if (musicOverride != null)
            {
                musicSource.clip = musicOverride;
            }
            else if (musicSource.clip == null)
            {
                musicSource.clip = ProceduralAudio.AmbientLoop();
            }
            musicSource.loop = true;
        }

        public void ApplyVolumes()
        {
            if (musicSource != null) musicSource.volume = GameSettings.MusicVolume * musicMix;
            if (sfxSource   != null) sfxSource.volume   = GameSettings.SfxVolume;
        }

        public void PlayMusic()
        {
            if (musicSource == null || musicSource.clip == null) return;
            if (!musicSource.isPlaying) musicSource.Play();
        }

        /// <summary>Baja la musica hasta silenciarla (para transiciones de escena).</summary>
        public System.Collections.IEnumerator FadeOutMusic(float duration)
        {
            if (musicSource == null) yield break;
            float from = musicSource.volume;
            yield return Tween.Value(duration, t => { if (musicSource != null) musicSource.volume = Mathf.Lerp(from, 0f, t); }, Ease.OutQuad);
            if (musicSource != null) musicSource.Stop();
        }

        void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }

        // -------------------------------------------------- atajos estaticos

        public static void Hover()   => Instance?.Play(Instance._hover, 1f, Random.Range(0.97f, 1.05f));
        public static void Click()   => Instance?.Play(Instance._click);
        public static void Back()    => Instance?.Play(Instance._back);
        public static void Confirm() => Instance?.Play(Instance._confirm);
        public static void Toggle()  => Instance?.Play(Instance._toggle, 1f, Random.Range(0.98f, 1.03f));
        public static void Deny()    => Instance?.Play(Instance._deny);

        /// <summary>Clic fino para los deslizadores; se limita para no saturar.</summary>
        public static void Tick()
        {
            if (Instance == null) return;
            if (Time.unscaledTime - Instance._lastTickTime < 0.045f) return;
            Instance._lastTickTime = Time.unscaledTime;
            Instance.Play(Instance._tick, 1f, Random.Range(0.94f, 1.08f));
        }
    }
}
