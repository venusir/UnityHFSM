using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphWindowSmoke
    {
        [Test]
        public void WindowBuildsForGraphAssetAndCleansUp()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            GameObject previousSelection = Selection.activeGameObject;
            Object previousObject = Selection.activeObject;

            StateMachineGraphWindow window = null;

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                Selection.activeObject = asset;

                window = ScriptableObject.CreateInstance<StateMachineGraphWindow>();

                Assert.IsNotNull(window);
                Assert.IsNotNull(window.rootVisualElement);
                Assert.Greater(window.rootVisualElement.childCount, 0);

                window.TogglePreviewPlayback();
                Assert.IsTrue(window.IsPreviewing);

                window.StepRuntime();

                window.TogglePreviewPlayback();
                window.TogglePreviewPlayback();
            }
            finally
            {
                if (window != null)
                    Object.DestroyImmediate(window);

                Selection.activeGameObject = previousSelection;
                Selection.activeObject = previousObject;
                Object.DestroyImmediate(asset);
            }
        }
    }
}
