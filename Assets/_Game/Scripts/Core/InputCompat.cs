using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ekkar.Core
{
    /// <summary>
    /// Capa fina sobre la entrada para que el menu funcione tanto con el
    /// Input System nuevo (que es el que usa este proyecto) como con el
    /// gestor antiguo, sin llenar de #if el resto del codigo.
    /// </summary>
    public static class InputCompat
    {
        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool HasPointer
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null;
#else
                return true;
#endif
            }
        }

        public static bool EscapePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        public static bool AnyInputThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
                if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
                return false;
#else
                return Input.anyKeyDown;
#endif
            }
        }
    }
}
