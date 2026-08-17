using System.Collections;
using Ekkar.Audio;
using Ekkar.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// "Hoguera temporal": un engranaje suspendido que empieza apagado y se
    /// enciende en cian cuando Ekkar pasa por delante. A partir de ahi, morir
    /// devuelve a este punto en vez de al principio del nivel.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] string id = "cp";
        [SerializeField] SpriteRenderer glow;
        [SerializeField] SpriteRenderer symbol;
        [SerializeField] Color offColor = new Color(0.35f, 0.32f, 0.5f, 0.45f);
        [SerializeField] Color onColor = new Color(0.13f, 0.83f, 0.93f, 1f);
        [SerializeField] float pulsePeriod = 2.4f;

        bool _active;

        public string Id => id;
        public Vector2 SpawnPosition => transform.position;

        void Reset()
        {
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.6f, 3f);
        }

        void Start()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;

            // si el jugador ya lo habia activado en una partida anterior,
            // arranca encendido
            if (GameProgress.LastCheckpointId == id &&
                GameProgress.SavedScene == SceneManager.GetActiveScene().name)
                _active = true;

            Paint(_active ? 1f : 0f);
        }

        void Update()
        {
            if (pulsePeriod < 0.01f) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / pulsePeriod);
            Paint(_active ? Mathf.Lerp(0.75f, 1f, k) : Mathf.Lerp(0f, 0.22f, k));
        }

        void Paint(float on)
        {
            Color c = Color.Lerp(offColor, onColor, on);
            if (symbol != null) symbol.color = c;
            if (glow != null)
            {
                Color g = onColor;
                g.a = Mathf.Lerp(0.05f, 0.45f, on);
                glow.color = g;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_active) return;
            if (other.GetComponentInParent<EkkarController>() == null) return;

            _active = true;
            GameProgress.Save(SceneManager.GetActiveScene().name, SpawnPosition, id);
            AudioManager.Confirm();

            // encender una hoguera cura del todo. Es lo que convierte llegar
            // hasta ella en un alivio y no solo en un punto de guardado
            var vida = other.GetComponentInParent<Damageable>();
            if (vida != null && vida.Lado == Damageable.Bando.Jugador)
            {
                int puesto = vida.Curar(vida.VidaMaxima);
                if (puesto > 0)
                    UI.PlayerHealthBar.Aviso("HOGUERA TEMPORAL", new Color(0.13f, 0.83f, 0.93f));
            }

            StartCoroutine(Flare());
        }

        IEnumerator Flare()
        {
            if (glow == null) yield break;
            Vector3 baseScale = glow.transform.localScale;
            yield return Tween.Value(0.55f, t =>
            {
                if (glow == null) return;
                glow.transform.localScale = baseScale * (1f + Ease.OutCubic(t) * 1.2f);
                Color c = onColor;
                c.a = (1f - Ease.OutQuad(t)) * 0.8f;
                glow.color = c;
            }, Ease.Linear);
            if (glow != null) glow.transform.localScale = baseScale;
        }
    }
}
