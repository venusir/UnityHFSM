using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    [CustomEditor(typeof(StateMachineGraphAsset))]
    public sealed class StateMachineGraphAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (StateMachineGraphAsset)target;
            asset.Graph.EnsureLists();

            EditorGUILayout.LabelField("UnityHFSM State Machine Graph", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Machines", asset.Graph.machines.Count.ToString());
            EditorGUILayout.LabelField("States", asset.Graph.states.Count.ToString());
            EditorGUILayout.LabelField("Transitions", asset.Graph.transitions.Count.ToString());
            EditorGUILayout.LabelField("Parameters", asset.Graph.parameters.Count.ToString());

            GraphValidationResult validation = GraphValidator.Validate(asset.Graph);
            int errors = 0;
            int warnings = 0;

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                if (validation.Issues[i].severity == GraphIssueSeverity.Error)
                    errors++;
                else
                    warnings++;
            }

            EditorGUILayout.HelpBox(
                errors == 0
                    ? "Validation: " + warnings + " warning(s)."
                    : "Validation: " + errors + " error(s), " + warnings + " warning(s).",
                errors == 0 ? MessageType.Info : MessageType.Error);

            if (asset.Graph.machines.Count == 0)
            {
                if (GUILayout.Button("Initialize Graph"))
                {
                    Undo.RegisterCompleteObjectUndo(asset, "Initialize Graph");
                    asset.SetGraph(GraphModelFactory.CreateNew());
                    EditorUtility.SetDirty(asset);
                }
            }

            if (GUILayout.Button("Open State Machine Editor"))
                StateMachineGraphWindow.OpenAsset(asset);

            if (GUILayout.Button("Export JSON"))
                GraphJsonEditorIO.Export(asset);

            if (GUILayout.Button("Import JSON"))
            {
                if (GraphJsonEditorIO.Import(asset))
                    Repaint();
            }
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId) as StateMachineGraphAsset;
            if (asset == null)
                return false;

            StateMachineGraphWindow.OpenAsset(asset);
            return true;
        }
    }
}
