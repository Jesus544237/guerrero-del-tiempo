using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// La pieza que faltaba para cerrar el bucle: cuando cae el campeon de la
    /// era, avisa a <see cref="LevelFlow"/> para que salga la pantalla de
    /// fragmento recuperado, o la de victoria si es el Senor del Tiempo.
    ///
    /// Espera un momento antes de avisar, para que dé tiempo a ver la
    /// animacion de muerte entera en vez de cortarla con el rotulo.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class BossDefeat : MonoBehaviour
    {
        [SerializeField] bool esJefeFinal = false;
        [SerializeField] float esperaAntesDeAvisar = 1.8f;
        [SerializeField] string rotulo = "";

        Damageable _vida;

        void Awake()
        {
            _vida = GetComponent<Damageable>();
            _vida.Muerto_ += AlCaer;
        }

        void OnDestroy()
        {
            if (_vida != null) _vida.Muerto_ -= AlCaer;
        }

        void AlCaer(Damageable _)
        {
            if (!string.IsNullOrEmpty(rotulo))
                UI.PlayerHealthBar.Aviso(rotulo, new Color(0.98f, 0.75f, 0.14f));
            Audio.Sfx.Play("chrono", 1f, 0.7f);
            Invoke(nameof(Avisar), esperaAntesDeAvisar);
        }

        void Avisar()
        {
            var flujo = FindAnyObjectByType<LevelFlow>();
            if (flujo == null)
            {
                Debug.LogWarning("[Ekkar] No hay LevelFlow en la escena: el jefe cae y no pasa nada.");
                return;
            }

            // el jefe final termina el juego; los mini-jefes solo abren la meta
            if (esJefeFinal) flujo.BossDefeated();
            else flujo.LevelCleared();
        }
    }
}
