using UnityEditor;
using UnityEngine;
using UnityHFSM;

namespace UnityHFSM.EditorTools
{
    /// <summary>
    /// Minimal editor window that verifies the editor assembly can reference the
    /// runtime assembly. This is the starting point for the visual state machine editor.
    /// </summary>
    public class DevelopmentInfoWindow : EditorWindow
    {
        [MenuItem("Tools/UnityHFSM/Development Info")]
        public static void Open()
        {
            GetWindow<DevelopmentInfoWindow>("UnityHFSM");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UnityHFSM development project", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Runtime assembly", typeof(StateBase).Assembly.GetName().Name);
            EditorGUILayout.LabelField("Assembly path", typeof(StateBase).Assembly.Location);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This window is a placeholder for the visual state machine editor. "
                + "Editor tooling belongs in Assets/UnityHFSM/Editor.",
                MessageType.Info);
        }
    }
}
