using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ekkar.UI
{
    /// <summary>
    /// La cinematica que abre el juego.
    ///
    /// Reproduce un video a pantalla completa y, cuando termina o cuando el
    /// jugador pulsa cualquier cosa, pasa al menu principal.
    ///
    /// Lo importante: si no hay video, no se queda colgada. Salta directa al
    /// menu. Asi se puede dejar la escena montada en el build desde hoy y
    /// meter el archivo cuando este listo, sin tocar nada mas.
    ///
    /// El video se busca en dos sitios, por este orden:
    ///   1. el clip asignado en el inspector,
    ///   2. StreamingAssets/intro.mp4 — basta con copiar el archivo ahi.
    /// El segundo es el comodo: no hay que importar nada ni volver a compilar,
    /// y permite cambiar la cinematica en un juego ya publicado.
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        [SerializeField] VideoClip clip;
        [SerializeField] string archivoEnStreamingAssets = "intro.mp4";
        [SerializeField] string escenaSiguiente = "MainMenu";
        [Tooltip("Segundos antes de que se pueda saltar, para que no se salte sin querer.")]
        [SerializeField] float esperaAntesDeSaltar = 0.6f;
        [Tooltip("Corta sola si el video se atasca.")]
        [SerializeField] float tiempoMaximo = 40f;

        VideoPlayer _video;
        RawImage _pantalla;
        TextMeshProUGUI _aviso;
        RenderTexture _rt;
        float _desde;
        bool _saliendo;

        void Start()
        {
            _desde = Time.unscaledTime;
            Construye();

            string url = RutaDelVideo();
            if (clip == null && string.IsNullOrEmpty(url))
            {
                Debug.Log("[Ekkar] No hay cinematica todavia: voy directo al menu.");
                Termina();
                return;
            }

            _rt = new RenderTexture(1920, 1080, 0);
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _rt;
            _pantalla.texture = _rt;

            if (clip != null) { _video.source = VideoSource.VideoClip; _video.clip = clip; }
            else { _video.source = VideoSource.Url; _video.url = url; }

            _video.audioOutputMode = VideoAudioOutputMode.Direct;
            _video.isLooping = false;
            _video.loopPointReached += _ => Termina();
            _video.errorReceived += (_, e) =>
            {
                Debug.LogWarning($"[Ekkar] La cinematica no se pudo reproducir: {e}");
                Termina();
            };
            _video.Play();
        }

        void OnDestroy()
        {
            if (_rt == null) return;
            _rt.Release();
            Destroy(_rt);
        }

        string RutaDelVideo()
        {
            if (string.IsNullOrEmpty(archivoEnStreamingAssets)) return null;
            string ruta = Path.Combine(Application.streamingAssetsPath, archivoEnStreamingAssets);
            // en Android streamingAssets vive dentro del apk y File.Exists miente
            if (Application.platform == RuntimePlatform.Android) return ruta;
            return File.Exists(ruta) ? ruta : null;
        }

        void Update()
        {
            if (_saliendo) return;

            float lleva = Time.unscaledTime - _desde;
            if (lleva > tiempoMaximo) { Termina(); return; }
            if (lleva < esperaAntesDeSaltar) return;

            if (_aviso != null)
                _aviso.color = new Color(0.886f, 0.906f, 0.922f,
                                         0.35f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f)) * 0.4f);

            if (AlgoPulsado()) Termina();
        }

        void Termina()
        {
            if (_saliendo) return;
            _saliendo = true;

            if (_video != null) _video.Stop();

            if (Application.CanStreamedLevelBeLoaded(escenaSiguiente))
            {
                SceneManager.LoadScene(escenaSiguiente);
                return;
            }
            Debug.LogWarning($"[Ekkar] '{escenaSiguiente}' no esta en la lista de escenas del build.");
        }

        void Construye()
        {
            _video = gameObject.GetComponent<VideoPlayer>();
            if (_video == null) _video = gameObject.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.waitForFirstFrame = true;
            _video.skipOnDrop = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            var escala = gameObject.GetComponent<CanvasScaler>();
            if (escala == null) escala = gameObject.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);

            var negro = new GameObject("Negro", typeof(RectTransform));
            negro.transform.SetParent(transform, false);
            var img = negro.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            Pantalla(img.rectTransform);

            var video = new GameObject("Video", typeof(RectTransform));
            video.transform.SetParent(transform, false);
            _pantalla = video.AddComponent<RawImage>();
            _pantalla.raycastTarget = false;
            Pantalla(_pantalla.rectTransform);

            var go = new GameObject("Saltar", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _aviso = go.AddComponent<TextMeshProUGUI>();
            _aviso.text = "pulsa cualquier tecla para saltar";
            _aviso.fontSize = 24f;
            _aviso.alignment = TextAlignmentOptions.Right;
            _aviso.raycastTarget = false;
            var rt = _aviso.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(700f, 34f);
            rt.anchoredPosition = new Vector2(-60f, 48f);
        }

        static void Pantalla(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        static bool AlgoPulsado()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }
#else
        static bool AlgoPulsado() => Input.anyKeyDown;
#endif
    }
}

