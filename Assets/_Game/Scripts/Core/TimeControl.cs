using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ekkar.Core
{
    /// <summary>
    /// El reloj del mundo. Cuando Ekkar detiene el tiempo, todo lo que no sea
    /// el se queda clavado: los enemigos, sus animaciones y las saetas que ya
    /// venian volando.
    ///
    /// Es un estado global a proposito. La alternativa era que cada enemigo
    /// preguntase por ahi si el tiempo esta parado, y eso significa buscar al
    /// jugador cada fotograma; asi solo hay una variable que mirar.
    ///
    /// No se toca Time.timeScale porque eso pararia tambien a Ekkar, y la
    /// gracia de la habilidad es justo la contraria: el mundo se para y tu no.
    /// </summary>
    public static class TimeControl
    {
        static float _hasta;
        static float _total;

        /// <summary>El tiempo esta parado ahora mismo.</summary>
        public static bool Detenido => Time.time < _hasta;

        /// <summary>Segundos que le quedan a la habilidad.</summary>
        public static float Restante => Mathf.Max(0f, _hasta - Time.time);

        /// <summary>De 1 a 0 segun se agota. Lo usa la barra de la pantalla.</summary>
        public static float Fraccion => _total > 0.001f ? Mathf.Clamp01(Restante / _total) : 0f;

        public static float Total => _total;

        /// <summary>Salta al empezar la parada; la pantalla se engancha aqui.</summary>
        public static event Action<float> Empieza;

        public static void Detener(float segundos)
        {
            if (segundos <= 0f) return;
            _total = segundos;
            _hasta = Time.time + segundos;
            Empieza?.Invoke(segundos);
        }

        /// <summary>Corta la parada antes de tiempo (muerte, cambio de escena).</summary>
        public static void Reiniciar()
        {
            _hasta = 0f;
            _total = 0f;
        }

        // Los estaticos sobreviven al Play si esta desactivada la recarga de
        // dominio, y tambien al cambio de escena. Se limpian en los dos casos.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AlArrancar()
        {
            _hasta = 0f;
            _total = 0f;
            Empieza = null;
            SceneManager.sceneLoaded -= AlCargarEscena;
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        static void AlCargarEscena(Scene _, LoadSceneMode __)
        {
            _hasta = 0f;
            _total = 0f;
        }
    }
}
