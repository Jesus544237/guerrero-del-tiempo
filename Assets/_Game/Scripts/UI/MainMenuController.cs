using System.Collections;
using System.Collections.Generic;
using Ekkar.Audio;
using Ekkar.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ekkar.UI
{
    /// <summary>
    /// Cerebro del menu principal: reproduce la secuencia de entrada, conecta
    /// los cuatro botones con sus acciones y gestiona los paneles modales y las
    /// transiciones de escena.
    ///
    /// Los metodos publicos (Jugar, AbrirOpciones, AbrirCreditos, Salir...) son
    /// los que quedan enlazados en el OnClick de cada boton desde el Inspector.
    /// </summary>
    [AddComponentMenu("Ekkar/Main Menu Controller")]
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>Un elemento de la animacion de entrada del menu.</summary>
        [System.Serializable]
        public class IntroStep
        {
            public string label = "paso";
            public CanvasGroup group;
            public RectTransform move;
            public Vector2 fromOffset;
            public float delay;
            public float duration = 0.6f;
            public bool backEase;

            [HideInInspector] public Vector2 basePos;
        }

        [Header("Escena a cargar con JUGAR")]
        [Tooltip("Debe estar anadida en File > Build Profiles > Scene List.")]
        [SerializeField] string gameSceneName = "SampleScene";
        [Tooltip("Primera era del juego; es a donde lleva PARTIDA NUEVA.")]
        [SerializeField] string firstLevelScene = "01_EdadMedia_SitioEterno";

        [Header("Paneles")]
        [SerializeField] UIPanel optionsPanel;
        [SerializeField] UIPanel creditsPanel;
        [SerializeField] UIPanel quitPanel;

        [Header("Transicion")]
        [SerializeField] ScreenTransition transition;

        [Header("Menu")]
        [SerializeField] CanvasGroup menuGroup;
        [SerializeField] MenuButton playButton;
        [SerializeField] MenuButton[] mainButtons;

        [Header("Intro")]
        [SerializeField] bool playIntro = true;
        [SerializeField] List<IntroStep> introSteps = new List<IntroStep>();
        [SerializeField] TMPTextIntro[] textIntros;
        [SerializeField] float introFadeInTime = 0.9f;
        [SerializeField] MonoBehaviour[] enableAfterIntro;

        bool _busy;
        string _targetScene;
        bool _introRunning;
        bool _introSkipRequested;

        // ------------------------------------------------------------ ciclo

        void Awake()
        {
            foreach (var step in introSteps)
                if (step != null && step.move != null) step.basePos = step.move.anchoredPosition;

            if (enableAfterIntro != null)
                foreach (var mb in enableAfterIntro)
                    if (mb != null) mb.enabled = false;
        }

        void Start()
        {
            if (!GameSettings.Loaded) GameSettings.Load();
            GameSettings.ApplyAll();

            if (playIntro) StartCoroutine(IntroRoutine());
            else FinishIntro(true);
        }

        void Update()
        {
            if (_introRunning && InputCompat.AnyInputThisFrame)
                _introSkipRequested = true;

            if (!_introRunning && InputCompat.EscapePressed)
                HandleEscape();
        }

        void HandleEscape()
        {
            if (_busy) return;

            UIPanel open = TopOpenPanel();
            if (open != null)
            {
                AudioManager.Back();
                open.Close();
                return;
            }
            Salir();
        }

        UIPanel TopOpenPanel()
        {
            if (quitPanel != null && quitPanel.IsOpen) return quitPanel;
            if (creditsPanel != null && creditsPanel.IsOpen) return creditsPanel;
            if (optionsPanel != null && optionsPanel.IsOpen) return optionsPanel;
            return null;
        }

        // ------------------------------------------------------------ intro

        IEnumerator IntroRoutine()
        {
            _introRunning = true;
            _introSkipRequested = false;

            foreach (var step in introSteps)
            {
                if (step == null) continue;
                if (step.group != null) step.group.alpha = 0f;
                if (step.move != null) step.move.anchoredPosition = step.basePos + step.fromOffset;
            }

            if (transition != null) StartCoroutine(transition.FadeFromBlack(introFadeInTime));

            float longest = 0f;
            foreach (var step in introSteps)
            {
                if (step == null) continue;
                longest = Mathf.Max(longest, step.delay + step.duration);
                StartCoroutine(RunStep(step));
            }

            if (textIntros != null)
                foreach (var ti in textIntros)
                    if (ti != null) longest = Mathf.Max(longest, ti.TotalDuration);

            float t = 0f;
            while (t < longest && !_introSkipRequested)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            FinishIntro(_introSkipRequested);
        }

        IEnumerator RunStep(IntroStep step)
        {
            yield return Tween.Wait(step.delay);

            Vector2 from = step.basePos + step.fromOffset;
            yield return Tween.Value(step.duration, k =>
            {
                if (!_introRunning) return;
                if (step.group != null) step.group.alpha = Ease.OutQuad(Mathf.Clamp01(k * 1.4f));
                if (step.move != null)
                {
                    float e = step.backEase ? Ease.OutBackSoft(k) : Ease.OutCubic(k);
                    step.move.anchoredPosition = Vector2.LerpUnclamped(from, step.basePos, e);
                }
            }, Ease.Linear);
        }

        void FinishIntro(bool skipped)
        {
            // Las corrutinas de los pasos siguen vivas un instante, pero al
            // estar _introRunning en false ya no escriben nada.
            _introRunning = false;

            foreach (var step in introSteps)
            {
                if (step == null) continue;
                if (step.group != null) step.group.alpha = 1f;
                if (step.move != null) step.move.anchoredPosition = step.basePos;
            }

            if (skipped && textIntros != null)
                foreach (var ti in textIntros)
                    if (ti != null) ti.Complete();

            if (transition != null)
            {
                if (skipped) transition.ClearFade();
                transition.Block(false);
            }

            if (enableAfterIntro != null)
                foreach (var mb in enableAfterIntro)
                    if (mb != null) mb.enabled = true;

            SelectDefault();
        }

        /// <summary>Vuelve a lanzar la secuencia de entrada (util para ajustarla).</summary>
        public void ReproducirIntro()
        {
            if (_busy) return;
            StopAllCoroutines();
            if (textIntros != null)
                foreach (var ti in textIntros)
                    if (ti != null) ti.Play();
            StartCoroutine(IntroRoutine());
        }

        void SelectDefault()
        {
            if (playButton == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        // --------------------------------------------------- acciones (UI)

        /// <summary>JUGAR: continua la partida guardada si la hay, o empieza de cero.</summary>
        public void Jugar()
        {
            if (_busy) return;
            _busy = true;
            _targetScene = GameProgress.HasSave && !string.IsNullOrEmpty(GameProgress.SavedScene)
                ? GameProgress.SavedScene
                : firstLevelScene;
            StartCoroutine(PlayRoutine());
        }

        /// <summary>PARTIDA NUEVA: borra el progreso y arranca en la primera era.</summary>
        public void NuevaPartida()
        {
            if (_busy) return;
            _busy = true;
            GameProgress.Clear();
            _targetScene = firstLevelScene;
            StartCoroutine(PlayRoutine());
        }

        /// <summary>CONTINUAR: retoma donde se quedo.</summary>
        public void Continuar()
        {
            if (_busy) return;
            if (!GameProgress.HasSave) { AudioManager.Deny(); return; }
            _busy = true;
            _targetScene = string.IsNullOrEmpty(GameProgress.SavedScene)
                ? firstLevelScene
                : GameProgress.SavedScene;
            StartCoroutine(PlayRoutine());
        }

        /// <summary>true si hay partida guardada (para encender el boton CONTINUAR).</summary>
        public bool HayPartidaGuardada => GameProgress.HasSave;

        IEnumerator PlayRoutine()
        {
            SetMenuInteractable(false);
            AudioManager.Confirm();

            if (transition != null) StartCoroutine(transition.ChronoFlash(0.5f));
            yield return Tween.Wait(0.2f);

            if (AudioManager.Instance != null)
                StartCoroutine(AudioManager.Instance.FadeOutMusic(0.7f));

            if (transition != null) yield return transition.FadeToBlack(0.7f);

            string target = string.IsNullOrEmpty(_targetScene) ? gameSceneName : _targetScene;
            if (!string.IsNullOrEmpty(target) && Application.CanStreamedLevelBeLoaded(target))
            {
                SceneManager.LoadScene(target);
                yield break;
            }

            Debug.LogWarning($"[Ekkar] La escena '{target}' todavia no esta en la lista de escenas del build. " +
                             "Anadela en File > Build Profiles (Scene List) y el boton JUGAR la cargara.");

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ApplyVolumes();
                AudioManager.Instance.PlayMusic();
            }
            if (transition != null) yield return transition.FadeFromBlack(0.5f);

            SetMenuInteractable(true);
            _busy = false;
            SelectDefault();
        }

        /// <summary>OPCIONES: abre el panel de ajustes.</summary>
        public void AbrirOpciones()
        {
            if (_busy || TopOpenPanel() != null) return;
            optionsPanel?.Open();
        }

        /// <summary>CREDITOS: abre el panel de creditos.</summary>
        public void AbrirCreditos()
        {
            if (_busy || TopOpenPanel() != null) return;
            creditsPanel?.Open();
        }

        /// <summary>SALIR: pide confirmacion antes de cerrar el juego.</summary>
        public void Salir()
        {
            if (_busy || TopOpenPanel() != null) return;
            quitPanel?.Open();
        }

        /// <summary>Confirmacion del dialogo de salida.</summary>
        public void ConfirmarSalida()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(QuitRoutine());
        }

        /// <summary>Cancelacion del dialogo de salida.</summary>
        public void CancelarSalida()
        {
            AudioManager.Back();
            quitPanel?.Close();
        }

        /// <summary>Cierra el panel que este abierto (util para botones "CERRAR").</summary>
        public void CerrarPanelActivo()
        {
            UIPanel open = TopOpenPanel();
            if (open == null) return;
            AudioManager.Back();
            open.Close();
        }

        IEnumerator QuitRoutine()
        {
            SetMenuInteractable(false);
            quitPanel?.Close();
            AudioManager.Confirm();

            if (AudioManager.Instance != null)
                StartCoroutine(AudioManager.Instance.FadeOutMusic(0.8f));

            if (transition != null) yield return transition.FadeToBlack(0.8f);

            Debug.Log("[Ekkar] El tiempo se ha detenido. Cerrando el juego.");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void SetMenuInteractable(bool value)
        {
            if (menuGroup != null)
            {
                menuGroup.interactable = value;
                menuGroup.blocksRaycasts = value;
            }
            if (mainButtons != null)
                foreach (var b in mainButtons)
                    if (b != null) b.interactable = value;
        }
    }
}
