using Ekkar.UI;
using UnityEditor;
using UnityEditor.UI;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Los componentes que heredan de Selectable usan por defecto el inspector
    /// de Unity, que solo dibuja los campos de Selectable. Estos editores
    /// anaden debajo los campos propios de cada widget del menu.
    /// </summary>
    public abstract class EkkarSelectableEditor : SelectableEditor
    {
        static readonly string[] k_Hidden =
        {
            "m_Script", "m_Navigation", "m_Transition", "m_Colors", "m_SpriteState",
            "m_AnimationTriggers", "m_Interactable", "m_TargetGraphic"
        };

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space(8f);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, k_Hidden);
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(MenuButton), true), CanEditMultipleObjects]
    public class MenuButtonEditor : EkkarSelectableEditor { }

    [CustomEditor(typeof(OptionSelector), true), CanEditMultipleObjects]
    public class OptionSelectorEditor : EkkarSelectableEditor { }

    [CustomEditor(typeof(PixelToggle), true), CanEditMultipleObjects]
    public class PixelToggleEditor : EkkarSelectableEditor { }
}
