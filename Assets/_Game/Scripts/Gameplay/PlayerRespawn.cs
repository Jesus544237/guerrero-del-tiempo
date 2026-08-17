using System.Collections;
using Ekkar.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Muerte y reaparicion de Ekkar. Al morir se reproduce la animacion,
    /// se funde a negro y se vuelve al ultimo punto de guardado activado; si
    /// no hay ninguno, al inicio del nivel.
    ///
    /// Llamar a <see cref="Die"/> desde el dano de los enemigos o desde una
    /// zona de caida.
    /// </summary>
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] EkkarController controller;
        [SerializeField] Animator animator;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] float deathAnimTime = 1.6f;
        [SerializeField] float fadeTime = 0.5f;
        [SerializeField] float killPlaneY = -12f;
        [SerializeField] Vector2 levelStart = new Vector2(6f, 0.2f);

        Rigidbody2D _rb;
        bool _dying;

        public bool IsDying => _dying;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (controller == null) controller = GetComponent<EkkarController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        }

        void Start()
        {
            // arranca en el ultimo punto de guardado de esta escena
            string scene = SceneManager.GetActiveScene().name;
            if (GameProgress.HasCheckpointFor(scene))
                transform.position = GameProgress.GetCheckpoint(levelStart);
        }

        void Update()
        {
            if (!_dying && transform.position.y < killPlaneY) Die();
        }

        public void Die()
        {
            if (_dying) return;
            _dying = true;
            GameProgress.CountDeath();
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            if (controller != null) controller.enabled = false;

            // Damageable apaga los colliders al morir, asi que sin esto Ekkar
            // se cae por el suelo mientras suena la animacion de muerte: se veia
            // desaparecer hacia abajo en vez de verse morir. Se le quita la
            // fisica y se queda donde cayo.
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (animator != null) animator.Play("death", 0, 0f);

            // si murio con el tiempo parado, el mundo vuelve a andar: si no,
            // los enemigos seguirian clavados durante toda la pantalla de muerte
            TimeControl.Reiniciar();

            // lo que dura el clip de verdad, no un numero escrito a mano: la
            // muerte dura 2,45 s y se cortaba a los 1,6
            yield return Tween.Wait(Mathf.Max(deathAnimTime, DuraLaMuerte()));

            // si el nivel tiene pantalla de resultado, manda ella: muestra la
            // derrota y el boton REINTENTAR recarga la escena, que arranca en
            // la ultima hoguera activada
            var flow = FindAnyObjectByType<LevelFlow>();
            if (flow != null)
            {
                flow.PlayerDefeated();
                yield break;
            }

            // fundido de salida
            yield return Tween.Value(fadeTime, t => SetAlpha(1f - t), Ease.OutQuad);

            string scene = SceneManager.GetActiveScene().name;
            transform.position = GameProgress.HasCheckpointFor(scene)
                ? (Vector3)GameProgress.GetCheckpoint(levelStart)
                : (Vector3)levelStart;

            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.linearVelocity = Vector2.zero;
            }
            if (animator != null) animator.Play("idle", 0, 0f);

            // vuelve entero: vida, cadencia y mana. Sin esto Ekkar reaparecia
            // marcado como muerto y con la barra a cero, y ya no volvia a pegar
            var combate = GetComponent<PlayerCombat>();
            if (combate != null) combate.Revivir();

            yield return Tween.Value(fadeTime, t => SetAlpha(t), Ease.OutQuad);

            if (controller != null) controller.enabled = true;
            _dying = false;
        }

        /// <summary>Lo que dura el clip de muerte, sacado del propio Animator.</summary>
        float DuraLaMuerte()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return 0f;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == "death") return clip.length;
            return 0f;
        }

        void SetAlpha(float a)
        {
            if (sprite == null) return;
            Color c = sprite.color;
            c.a = Mathf.Clamp01(a);
            sprite.color = c;
        }
    }
}
