using UnityEngine;

namespace Ekkar.Core
{
    /// <summary>
    /// Punto de guardado del juego. Se queda con la ultima hoguera temporal
    /// que Ekkar activo, para que morir devuelva al jugador ahi y no al
    /// principio del nivel. Persiste en PlayerPrefs, asi que tambien sobrevive
    /// a cerrar el juego.
    /// </summary>
    public static class GameProgress
    {
        const string K_SCENE = "ekkar.save.scene";
        const string K_X     = "ekkar.save.x";
        const string K_Y     = "ekkar.save.y";
        const string K_ID    = "ekkar.save.checkpoint";
        const string K_DEATHS = "ekkar.save.deaths";

        const string K_LEVEL = "ekkar.save.level";
        const string K_CLEARED = "ekkar.save.cleared";

        public static string SavedScene => PlayerPrefs.GetString(K_SCENE, "");
        public static int Deaths => PlayerPrefs.GetInt(K_DEATHS, 0);

        /// <summary>Nivel mas avanzado al que ha llegado el jugador (1 = Edad Media).</summary>
        public static int ReachedLevel => PlayerPrefs.GetInt(K_LEVEL, 0);

        /// <summary>Ultimo nivel terminado. 0 = ninguno.</summary>
        public static int ClearedLevel => PlayerPrefs.GetInt(K_CLEARED, 0);

        /// <summary>Hay una partida que continuar.</summary>
        public static bool HasSave => !string.IsNullOrEmpty(SavedScene) || ReachedLevel > 0;

        /// <summary>Se llama al entrar en un nivel.</summary>
        public static void ReachLevel(int level, string sceneName)
        {
            if (level > ReachedLevel) PlayerPrefs.SetInt(K_LEVEL, level);
            PlayerPrefs.SetString(K_SCENE, sceneName);
            PlayerPrefs.Save();
        }

        /// <summary>Se llama al terminar un nivel.</summary>
        public static void CompleteLevel(int level)
        {
            if (level > ClearedLevel) PlayerPrefs.SetInt(K_CLEARED, level);
            PlayerPrefs.Save();
        }

        /// <summary>Hay un punto de guardado utilizable para esta escena.</summary>
        public static bool HasCheckpointFor(string sceneName)
        {
            return PlayerPrefs.HasKey(K_X) && SavedScene == sceneName;
        }

        public static Vector2 GetCheckpoint(Vector2 fallback)
        {
            if (!PlayerPrefs.HasKey(K_X)) return fallback;
            return new Vector2(PlayerPrefs.GetFloat(K_X), PlayerPrefs.GetFloat(K_Y));
        }

        public static string LastCheckpointId => PlayerPrefs.GetString(K_ID, "");

        public static void Save(string sceneName, Vector2 position, string checkpointId)
        {
            PlayerPrefs.SetString(K_SCENE, sceneName);
            PlayerPrefs.SetFloat(K_X, position.x);
            PlayerPrefs.SetFloat(K_Y, position.y);
            PlayerPrefs.SetString(K_ID, checkpointId ?? "");
            PlayerPrefs.Save();
        }

        public static void CountDeath()
        {
            PlayerPrefs.SetInt(K_DEATHS, Deaths + 1);
            PlayerPrefs.Save();
        }

        /// <summary>Borra el progreso (para "Partida nueva" desde el menu).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(K_SCENE);
            PlayerPrefs.DeleteKey(K_X);
            PlayerPrefs.DeleteKey(K_Y);
            PlayerPrefs.DeleteKey(K_ID);
            PlayerPrefs.DeleteKey(K_DEATHS);
            PlayerPrefs.DeleteKey(K_LEVEL);
            PlayerPrefs.DeleteKey(K_CLEARED);
            PlayerPrefs.Save();
        }
    }
}
