using NUnit.Framework;
using UnityEngine;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphCommandGuards
    {
        [Test]
        public void AuthoringCommandsAreBlockedWhilePreviewing()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            var windowState = new GraphEditorWindowState { asset = asset };
            var session = new GraphEditorSession(windowState);

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                session.Initialize();
                var commands = new GraphEditorCommands(session);

                int stateCount = asset.Graph.states.Count;
                Assert.IsTrue(commands.Runtime.StartPreview().Success);

                GraphCommandResult create = commands.Authoring.CreateState(Vector2.zero);
                Assert.IsFalse(create.Success);
                Assert.AreEqual(GraphCommandBlockReason.ReadOnlyMode, create.Reason);
                Assert.AreEqual(stateCount, asset.Graph.states.Count);

                Assert.IsTrue(commands.Runtime.StopPreview().Success);
            }
            finally
            {
                session.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void InvalidRenameIsReportedWithoutMutatingTheModel()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            var session = new GraphEditorSession(new GraphEditorWindowState { asset = asset });

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                session.Initialize();
                var commands = new GraphEditorCommands(session);
                string stateId = asset.Graph.states[0].id;
                string originalName = asset.Graph.states[0].name;

                GraphCommandResult result = commands.Authoring.RenameState(stateId, " ");

                Assert.IsFalse(result.Success);
                Assert.AreEqual(GraphCommandBlockReason.InvalidName, result.Reason);
                Assert.AreEqual(originalName, asset.Graph.states[0].name);
            }
            finally
            {
                session.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void LiveValuesRequireARuntime()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            var session = new GraphEditorSession(new GraphEditorWindowState { asset = asset });

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                session.Initialize();
                var commands = new GraphEditorCommands(session);

                GraphCommandResult result = commands.Runtime.SetFloat("Speed", 1f);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(GraphCommandBlockReason.ReadOnlyMode, result.Reason);
            }
            finally
            {
                session.Dispose();
                Object.DestroyImmediate(asset);
            }
        }
    }
}
