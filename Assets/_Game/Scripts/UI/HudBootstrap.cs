using Ekkar.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Ekkar.UI
{
    /// <summary>
    /// Monta el HUD solo en cualquier escena donde haya un Ekkar.
    ///
    /// Antes cada pieza nueva de interfaz habia que anadirla a mano al objeto
    /// 10_HUD desde el comando de poblar la escena, y cuatro escenas por cuatro
    /// componentes son dieciseis oportunidades de que se te olvide una y luego
    /// te preguntes por que el menu de pausa no sale. Esto lo cierra: si falta
    /// algo, se anade al cargar la escena.
    ///
    /// Lo que ya este puesto en la escena se respeta tal cual, con sus ajustes
    /// del inspector.
    /// </summary>
    public static class HudBootstrap
    {
        const string Nombre = "10_HUD";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arranca()
        {
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
            Instala();
        }

        static void AlCargar(Scene _, LoadSceneMode __) => Instala();

        static void Instala()
        {
            // sin Ekkar no es una escena de juego: el menu principal tiene lo suyo
            if (Object.FindAnyObjectByType<EkkarController>() == null) return;

            var hud = GameObject.Find(Nombre);
            if (hud == null) hud = new GameObject(Nombre);

            var fuente = BuscaFuente();

            var barra = Object.FindAnyObjectByType<PlayerHealthBar>();
            if (barra == null)
            {
                barra = hud.AddComponent<PlayerHealthBar>();
                barra.fuente = fuente;
            }

            if (Object.FindAnyObjectByType<AbilityBar>() == null)
                hud.AddComponent<AbilityBar>().fuente = fuente;

            if (Object.FindAnyObjectByType<TimeStopOverlay>() == null)
                hud.AddComponent<TimeStopOverlay>().fuente = fuente;

            if (Object.FindAnyObjectByType<PauseMenu>() == null)
                hud.AddComponent<PauseMenu>().fuente = fuente;

            AseguraEventSystem();
        }

        /// <summary>
        /// La tipografia pixel no esta en Resources, asi que no se puede cargar
        /// por nombre en tiempo de ejecucion. Se copia de la primera pieza de
        /// interfaz que ya la tenga puesta desde el editor, y si no hay ninguna
        /// se cae en la de TextMeshPro para que al menos se lea.
        /// </summary>
        static TMP_FontAsset BuscaFuente()
        {
            var barra = Object.FindAnyObjectByType<PlayerHealthBar>();
            if (barra != null && barra.fuente != null) return barra.fuente;

            var pausa = Object.FindAnyObjectByType<PauseMenu>();
            if (pausa != null && pausa.fuente != null) return pausa.fuente;

            var habilidades = Object.FindAnyObjectByType<AbilityBar>();
            if (habilidades != null && habilidades.fuente != null) return habilidades.fuente;

            var suelta = Object.FindAnyObjectByType<TextMeshProUGUI>();
            if (suelta != null && suelta.font != null) return suelta.font;

            return TMP_Settings.defaultFontAsset;
        }

        /// <summary>Sin EventSystem los botones del menu no responden al raton.</summary>
        static void AseguraEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
