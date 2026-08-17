using Ekkar.Core;
using Ekkar.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Hilo conductor de un nivel: sabe cual es, a cual se va despues, y quien
    /// muestra la pantalla de resultado cuando Ekkar cae, termina la era o
    /// derrota al Senor del Tiempo.
    ///
    /// La meta se marca con un trigger al final del recorrido; el jefe final
    /// llama a <see cref="BossDefeated"/>.
    /// </summary>
    public class LevelFlow : MonoBehaviour
    {
        [Header("Identidad")]
        [SerializeField] int levelIndex = 1;                 // 1 = Edad Media
        [SerializeField] string nextSceneName = "";          // vacio = ultimo nivel
        [SerializeField] bool isFinalBossLevel = false;

        [Header("Referencias")]
        [SerializeField] ResultScreen resultScreen;
        [SerializeField] PlayerRespawn player;

        public int LevelIndex => levelIndex;

        void Awake()
        {
            if (resultScreen == null) resultScreen = FindAnyObjectByType<ResultScreen>();
            if (player == null) player = FindAnyObjectByType<PlayerRespawn>();
        }

        void Start()
        {
            // al entrar en un nivel se recuerda hasta donde ha llegado el jugador
            GameProgress.ReachLevel(levelIndex, SceneManager.GetActiveScene().name);
        }

        /// <summary>Ekkar ha muerto y no le quedan reintentos automaticos.</summary>
        public void PlayerDefeated()
        {
            if (resultScreen != null) resultScreen.ShowDefeat();
        }

        /// <summary>Ha llegado al final de la era y se lleva el fragmento.</summary>
        public void LevelCleared()
        {
            if (resultScreen == null) return;
            if (resultScreen.IsShown) return;

            GameProgress.CompleteLevel(levelIndex);

            if (isFinalBossLevel) resultScreen.ShowVictory();
            else resultScreen.ShowLevelClear();
        }

        /// <summary>El Senor del Tiempo ha caido: final del juego.</summary>
        public void BossDefeated()
        {
            if (resultScreen == null) return;
            GameProgress.CompleteLevel(levelIndex);
            resultScreen.ShowVictory();
        }

        public void LoadNextLevel()
        {
            if (!string.IsNullOrEmpty(nextSceneName) &&
                Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }

            Debug.LogWarning($"[Ekkar] No hay siguiente escena ('{nextSceneName}'); vuelvo al menu.");
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
        }
    }
}
