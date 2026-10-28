using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Editor-side JSON file and AssetDatabase GUID handling. The actual JSON
    /// shape is owned by the runtime GraphJsonCodec.
    /// </summary>
    public static class GraphJsonEditorIO
    {
        [MenuItem("Assets/Create/UnityHFSM/State Machine Graph")]
        public static void CreateGraphAsset()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            asset.SetGraph(GraphModelFactory.CreateNew());

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/StateMachineGraph.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            StateMachineGraphWindow.OpenAsset(asset);
        }

        public static void Export(StateMachineGraphAsset asset)
        {
            if (asset == null)
                return;

            string path = EditorUtility.SaveFilePanel(
                "Export State Machine Graph",
                string.Empty,
                asset.name + ".json",
                "json");

            if (string.IsNullOrEmpty(path))
                return;

            string json = GraphJsonCodec.ToJson(asset.Graph, behaviour =>
            {
                if (behaviour == null)
                    return string.Empty;

                return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    behaviour,
                    out string guid,
                    out long localId)
                    ? guid
                    : string.Empty;
            });

            File.WriteAllText(path, json);
            Debug.Log("UnityHFSM: exported graph JSON to " + path);
        }

        public static bool Import(StateMachineGraphAsset asset)
        {
            if (asset == null)
                return false;

            string path = EditorUtility.OpenFilePanel(
                "Import State Machine Graph",
                string.Empty,
                "json");

            if (string.IsNullOrEmpty(path))
                return false;

            string json = File.ReadAllText(path);
            GraphModel imported = GraphJsonCodec.FromJson(
                json,
                guid =>
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    return string.IsNullOrEmpty(assetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<StateBehaviour>(assetPath);
                },
                out GraphValidationResult issues);

            if (issues.HasErrors || imported == null)
            {
                LogIssues(issues);
                EditorUtility.DisplayDialog(
                    "UnityHFSM Import Failed",
                    "The graph JSON could not be imported. See the Console for details.",
                    "OK");
                return false;
            }

            GraphValidationResult validation = GraphValidator.Validate(imported);
            if (validation.HasErrors)
            {
                LogIssues(validation);
                EditorUtility.DisplayDialog(
                    "UnityHFSM Import Failed",
                    "The imported graph is invalid. See the Console for details.",
                    "OK");
                return false;
            }

            if (validation.HasWarnings)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "UnityHFSM Import Warnings",
                    "The imported graph has validation warnings. Import anyway?",
                    "Import",
                    "Cancel");

                if (!proceed)
                    return false;
            }

            Undo.RegisterCompleteObjectUndo(asset, "Import State Machine Graph");
            asset.SetGraph(imported);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("UnityHFSM: imported graph JSON from " + path);
            return true;
        }

        private static void LogIssues(GraphValidationResult result)
        {
            if (result == null)
                return;

            var builder = new StringBuilder("UnityHFSM graph import issues:");
            for (int i = 0; i < result.Issues.Count; i++)
            {
                GraphIssue issue = result.Issues[i];
                builder.Append('\n');
                builder.Append(issue.severity == GraphIssueSeverity.Error ? "ERROR " : "WARN ");
                builder.Append(issue.code);
                builder.Append(": ");
                builder.Append(issue.message);
            }

            Debug.LogError(builder.ToString());
        }
    }
}
