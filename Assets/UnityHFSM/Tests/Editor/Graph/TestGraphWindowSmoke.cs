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
                Assert.IsNotNull(window.Session);
                Assert.IsNotNull(window.Commands);
                Assert.IsTrue(window.Session.HasDocument);
                Assert.AreEqual(GraphEditorMode.Author, window.Session.Mode);

                string root = window.Session.CurrentMachineId;
                string firstState = asset.Graph.states[0].id;
                GraphCommandResult created = window.Commands.Authoring.CreateState(
                    new Vector2(200f, 0f));
                Assert.IsTrue(created.Success);
                Assert.AreEqual(GraphEditorSelectionKind.State, window.Session.Selection.Kind);

                GraphCommandResult connected = window.Commands.Authoring.Connect(
                    new GraphConnectionRequest(
                        GraphConnectSourceKind.State,
                        firstState,
                        GraphConnectTargetKind.State,
                        created.Value));
                Assert.IsTrue(connected.Success);
                Assert.AreEqual(GraphEditorSelectionKind.Transition, window.Session.Selection.Kind);

                Assert.IsTrue(window.Session.NavigateTo(
                    root,
                    firstState,
                    null,
                    true).Success);
                Assert.AreEqual(GraphEditorSelectionKind.State, window.Session.Selection.Kind);

                Assert.IsTrue(window.Commands.Runtime.StartPreview().Success);
                Assert.AreEqual(GraphEditorMode.Preview, window.Session.Mode);
                Assert.IsTrue(window.Session.IsPreviewing);

                Assert.IsTrue(window.Commands.Runtime.Step().Success);
                Assert.IsTrue(window.Commands.Runtime.StopPreview().Success);
                Assert.AreEqual(GraphEditorMode.Author, window.Session.Mode);
                Assert.IsFalse(window.Session.IsPreviewing);

                Assert.IsTrue(window.Session.NavigateTo(
                    root,
                    null,
                    connected.Value,
                    true).Success);
                Assert.AreEqual(GraphEditorSelectionKind.Transition, window.Session.Selection.Kind);
                Assert.IsTrue(window.Commands.Runtime.StartPreview().Success);
                Assert.IsTrue(window.Commands.Runtime.Step().Success);
                Assert.IsTrue(window.Commands.Runtime.StopPreview().Success);

                var runtime = new GraphRuntime(asset.Graph);
                try
                {
                    runtime.Start();
                    window.Session.SetPlayMode(true);
                    window.Session.RefreshLiveTargets();
                    Assert.AreEqual(GraphEditorMode.Live, window.Session.Mode);
                    Assert.AreEqual(1, window.Session.LiveTargets.Count);
                    Assert.IsTrue(window.Commands.Runtime.TogglePause().Success);
                    Assert.IsTrue(window.Commands.Runtime.Step().Success);
                }
                finally
                {
                    window.Session.SetPlayMode(false);
                    runtime.Dispose();
                }
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
