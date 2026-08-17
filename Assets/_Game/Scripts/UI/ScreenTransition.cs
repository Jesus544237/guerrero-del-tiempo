using System.Collections;
using Ekkar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Velo de transicion a pantalla completa: fundido a negro y destello
    /// "chrono" en cian para el salto al juego.
    /// </summary>
    [AddComponentMenu("Ekkar/Screen Transition")]
    public class ScreenTransition : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fadeImage;
        [SerializeField] Image flashImage;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group != null) group.blocksRaycasts = false;
            SetFade(0f);
            SetFlash(0f);
        }

        void SetFade(float a)
        {
            if (fadeImage == null) return;
            Color c = fadeImage.color; c.a = a; fadeImage.color = c;
        }

        void SetFlash(float a)
        {
            if (flashImage == null) return;
            Color c = flashImage.color; c.a = a; flashImage.color = c;
        }

        public void Block(bool value)
        {
            if (group != null) group.blocksRaycasts = value;
        }

        /// <summary>Deja la pantalla limpia al instante (para saltarse la intro).</summary>
        public void ClearFade()
        {
            StopAllCoroutines();
            SetFade(0f);
            SetFlash(0f);
            Block(false);
        }

        /// <summary>Arranca en negro y abre a la escena.</summary>
        public IEnumerator FadeFromBlack(float duration = 0.7f)
        {
            Block(true);
            SetFade(1f);
            yield return Tween.Value(duration, t => SetFade(1f - Ease.OutQuad(t)), Ease.Linear);
            SetFade(0f);
            Block(false);
        }

        public IEnumerator FadeToBlack(float duration = 0.6f)
        {
            Block(true);
            yield return Tween.Value(duration, t => SetFade(Ease.InQuad(t)), Ease.Linear);
            SetFade(1f);
        }

        /// <summary>Fogonazo de energia temporal (para el boton JUGAR).</summary>
        public IEnumerator ChronoFlash(float duration = 0.45f)
        {
            Block(true);
            yield return Tween.Value(duration, t => SetFlash(Ease.Spike(t)), Ease.Linear);
            SetFlash(0f);
        }
    }
}
