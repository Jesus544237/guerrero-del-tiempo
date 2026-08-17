using System.Collections;
using Ekkar.Audio;
using Ekkar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Pantalla de resultado: derrota, fragmento conseguido al terminar una
    /// era, y victoria final sobre el Senor del Tiempo.
    ///
    /// Reutiliza la tipografia del titulo del menu, pero cada desenlace tiene
    /// su propio par de colores, asi que se reconoce de un vistazo en que era
    /// estas y si has ganado o perdido.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        public enum Kind { Derrota, NivelCompletado, Victoria }

        [Header("Partes")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Image scrim;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMPTextIntro titleIntro;
        [SerializeField] RectTransform buttonRow;
        [SerializeField] Selectable primaryButton;
        [SerializeField] Selectable secondaryButton;
        [SerializeField] TMP_Text primaryLabel;
        [SerializeField] TMP_Text secondaryLabel;
        [SerializeField] Image flash;
        [SerializeField] RectTransform rays;

        [Header("Colores por era (degradado del titulo)")]
        [SerializeField] Color eraTopColor = new Color(0.98f, 0.75f, 0.14f);
        [SerializeField] Color eraBottomColor = new Color(0.13f, 0.83f, 0.93f);

        [Header("Textos")]
        [SerializeField] string levelTitle = "FRAGMENTO RECUPERADO";
        [SerializeField] string levelSubtitle = "Una era vuelve a respirar.";

        Kind _kind;
        bool _shown;

        public bool IsShown => _shown;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            Hide();
        }

        void Hide()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ mostrar

        public void ShowDefeat() => Show(Kind.Derrota);
        public void ShowLevelClear() => Show(Kind.NivelCompletado);
        public void ShowVictory() => Show(Kind.Victoria);

        public void Show(Kind kind)
        {
            if (_shown) return;
            _shown = true;
            _kind = kind;
            gameObject.SetActive(true);
            StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            Color top, bottom;
            string title, subtitle, primary, secondary;

            switch (_kind)
            {
                case Kind.Derrota:
                    top = new Color(0.86f, 0.15f, 0.15f);
                    bottom = new Color(0.35f, 0.08f, 0.30f);
                    title = "EL TIEMPO TE ALCANZO";
                    subtitle = "Vuelves a la ultima hoguera temporal.";
                    primary = "REINTENTAR";
                    secondary = "MENU";
                    break;

                case Kind.Victoria:
                    top = new Color(1f, 0.85f, 0.25f);
                    bottom = new Color(0.13f, 0.90f, 0.95f);
                    title = "EL TIEMPO VUELVE A CORRER";
                    subtitle = "El Gran Reloj late otra vez. Las eras terminan su hora.";
                    primary = "VOLVER AL MENU";
                    secondary = "";
                    break;

                default:
                    top = eraTopColor;
                    bottom = eraBottomColor;
                    title = levelTitle;
                    subtitle = levelSubtitle;
                    primary = "CONTINUAR";
                    secondary = "MENU";
                    break;
            }

            ApplyText(title, subtitle, primary, secondary, top, bottom);

            // el mundo se detiene mientras se lee el resultado
            Time.timeScale = 0f;

            if (group != null) group.blocksRaycasts = true;

            if (_kind == Kind.Victoria) AudioManager.Confirm();
            else if (_kind == Kind.Derrota) AudioManager.Back();
            else AudioManager.Confirm();

            // velo + destello de entrada
            yield return Tween.Value(0.45f, t =>
            {
                if (group != null) group.alpha = Ease.OutQuad(t);
                if (scrim != null)
                {
                    Color c = scrim.color;
                    c.a = Ease.OutQuad(t) * (_kind == Kind.Derrota ? 0.92f : 0.80f);
                    scrim.color = c;
                }
                if (flash != null)
                {
                    Color f = top;
                    f.a = Ease.Spike(t) * (_kind == Kind.Victoria ? 0.55f : 0.2f);
                    flash.color = f;
                }
            }, Ease.Linear);

            if (flash != null) flash.color = new Color(top.r, top.g, top.b, 0f);

            if (titleIntro != null) titleIntro.Play();

            if (group != null) group.interactable = true;
            if (primaryButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);

            // los rayos solo giran en la victoria
            if (rays != null) rays.gameObject.SetActive(_kind == Kind.Victoria);
        }

        void ApplyText(string title, string subtitle, string primary, string secondary,
                       Color top, Color bottom)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.enableVertexGradient = true;
                titleText.colorGradient = new VertexGradient(top, top, bottom, bottom);
            }
            if (subtitleText != null) subtitleText.text = subtitle;
            if (primaryLabel != null) primaryLabel.text = primary;

            bool hasSecondary = !string.IsNullOrEmpty(secondary);
            if (secondaryButton != null) secondaryButton.gameObject.SetActive(hasSecondary);
            if (secondaryLabel != null && hasSecondary) secondaryLabel.text = secondary;
        }

        void Update()
        {
            // los rayos de la victoria giran despacio, en tiempo real
            if (rays != null && rays.gameObject.activeSelf)
                rays.Rotate(0f, 0f, 8f * Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------ acciones

        /// <summary>Boton principal: reintentar, seguir al siguiente nivel o volver al menu.</summary>
        public void OnPrimary()
        {
            Time.timeScale = 1f;

            switch (_kind)
            {
                case Kind.Derrota:
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    break;

                case Kind.Victoria:
                    LoadMenu();
                    break;

                default:
                    var flow = FindAnyObjectByType<Gameplay.LevelFlow>();
                    if (flow != null) flow.LoadNextLevel();
                    else LoadMenu();
                    break;
            }
        }

        public void OnSecondary()
        {
            Time.timeScale = 1f;
            LoadMenu();
        }

        static void LoadMenu()
        {
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
            else Debug.LogWarning("[Ekkar] MainMenu no esta en la lista de escenas del build.");
        }
    }
}
