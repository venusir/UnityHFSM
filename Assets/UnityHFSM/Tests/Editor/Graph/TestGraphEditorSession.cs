using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphEditorSession
    {
        private StateMachineGraphAsset asset;
        private GraphEditorWindowState state;
        private GraphEditorSession session;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            asset.SetGraph(GraphModelFactory.CreateNew());
            state = new GraphEditorWindowState { asset = asset };
            session = new GraphEditorSession(state);
            session.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            session?.Dispose();
            if (asset != null)
                Object.DestroyImmediate(asset);
        }

        [Test]
        public void InitializesInAuthorModeAtRoot()
        {
            Assert.AreSame(asset, session.Asset);
            Assert.AreEqual(GraphEditorMode.Author, session.Mode);
            Assert.IsNotNull(session.Model);
            Assert.AreEqual(asset.Graph.rootMachineId, session.CurrentMachineId);
            Assert.AreEqual(GraphEditorSelectionKind.Machine, session.Selection.Kind);
        }

        [Test]
        public void PreviewStartsAndStopsWithModeChanges()
        {
            var changes = new List<GraphEditorChange>();
            session.Changed += changes.Add;

            Assert.IsTrue(session.StartPreview().Success);
            Assert.AreEqual(GraphEditorMode.Preview, session.Mode);
            Assert.IsTrue(session.IsPreviewing);
            Assert.IsTrue(changes.Contains(GraphEditorChange.Mode));

            Assert.IsTrue(session.StopPreview().Success);
            Assert.AreEqual(GraphEditorMode.Author, session.Mode);
            Assert.IsFalse(session.IsPreviewing);
            Assert.IsNull(session.Snapshot);
        }

        [Test]
        public void EnteringPlayModeStopsPreviewAndShowsLiveNoTarget()
        {
            Assert.IsTrue(session.StartPreview().Success);
            session.SetPlayMode(true);

            Assert.AreEqual(GraphEditorMode.LiveNoTarget, session.Mode);
            Assert.IsFalse(session.IsPreviewing);
        }

        [Test]
        public void LiveTargetMatchingEnablesLiveMode()
        {
            session.SetPlayMode(true);
            Assert.AreEqual(GraphEditorMode.LiveNoTarget, session.Mode);

            var runtime = new GraphRuntime(asset.Graph);
            try
            {
                session.RefreshLiveTargets();
                Assert.AreEqual(GraphEditorMode.Live, session.Mode);
                Assert.AreEqual(1, session.LiveTargets.Count);
                Assert.AreSame(runtime, session.CurrentRuntime);

                session.SetPlayMode(false);
                Assert.AreEqual(GraphEditorMode.Author, session.Mode);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void NavigationHistoryRestoresMachineAndSelection()
        {
            string root = asset.Graph.rootMachineId;
            string first = asset.Graph.states[0].id;
            string second = GraphModelFactory.AddState(
                asset.Graph,
                root,
                "Second",
                Vector2.zero).id;

            Assert.IsTrue(session.NavigateTo(root, first, null, true).Success);
            Assert.IsTrue(session.NavigateTo(root, second, null, true).Success);
            Assert.AreEqual(second, session.Selection.StateId);

            Assert.IsTrue(session.Back().Success);
            Assert.AreEqual(first, session.Selection.StateId);
            Assert.IsTrue(session.Forward().Success);
            Assert.AreEqual(second, session.Selection.StateId);
        }

        [Test]
        public void LockedSelectionFollowIsBlocked()
        {
            session.SetSelectionLocked(true);

            GraphCommandResult result = session.FollowSelection(asset);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GraphCommandBlockReason.ReadOnlyMode, result.Reason);
        }

        [Test]
        public void PreviewHostCannotChangeWhileRunning()
        {
            Assert.IsTrue(session.StartPreview().Success);

            GraphCommandResult result = session.SetPreviewHost(null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GraphCommandBlockReason.ReadOnlyMode, result.Reason);
        }

        [Test]
        public void UndoDuringPreviewReturnsToAuthor()
        {
            Assert.IsTrue(session.StartPreview().Success);

            session.OnUndoRedo();

            Assert.AreEqual(GraphEditorMode.Author, session.Mode);
            Assert.IsFalse(session.IsPreviewing);
        }
    }
}
