using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public static class StateMachineGraphAssetMenu
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
    }
}
