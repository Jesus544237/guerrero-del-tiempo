using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Meta del nivel. Al entrar Ekkar se da la era por terminada y aparece la
    /// pantalla de fragmento conseguido (o la de victoria, si es el nivel del
    /// jefe final).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class LevelGoal : MonoBehaviour
    {
        [SerializeField] LevelFlow flow;
        [Tooltip("Si esta puesto, la meta no se abre hasta que este muerto.")]
        [SerializeField] Damageable jefe;
        [SerializeField] string[] nombresJefe =
        {
            "caballero_ceniciento", "el_gran_yunque", "nemesis_digital",
            // faltaba el jefe final: en la Hora Cero se podia pasar de largo por
            // delante del Senor del Tiempo y ganar el juego sin pelearlo
            "senor_del_tiempo",
        };

        void Reset()
        {
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2f, 8f);
        }

        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            if (flow == null) flow = FindAnyObjectByType<LevelFlow>();
            if (jefe == null && nombresJefe != null)
                foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                    foreach (var n in nombresJefe)
                        if (d.name.StartsWith(n)) { jefe = d; break; }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (flow == null) return;
            if (other.GetComponentInParent<EkkarController>() == null) return;

            // la era no termina por llegar al final: hay que arrancarle la
            // pieza del Gran Reloj al campeon que la guarda
            if (jefe != null && !jefe.Muerto)
            {
                Audio.Sfx.Play("impacto", 0.5f, 0.7f);
                Debug.Log("[Ekkar] La campana sigue sonando: vence primero al campeon de esta era.");
                return;
            }
            flow.LevelCleared();
        }
    }
}
