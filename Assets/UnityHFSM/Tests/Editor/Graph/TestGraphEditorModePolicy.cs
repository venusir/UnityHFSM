using NUnit.Framework;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphEditorModePolicy
    {
        [Test]
        public void ResolvesModeFromDocumentPlayAndRuntimeState()
        {
            Assert.AreEqual(
                GraphEditorMode.NoDocument,
                GraphEditorModePolicy.Resolve(false, false, false, false));
            Assert.AreEqual(
                GraphEditorMode.Author,
                GraphEditorModePolicy.Resolve(true, false, false, false));
            Assert.AreEqual(
                GraphEditorMode.Preview,
                GraphEditorModePolicy.Resolve(true, false, true, false));
            Assert.AreEqual(
                GraphEditorMode.Live,
                GraphEditorModePolicy.Resolve(true, true, false, true));
            Assert.AreEqual(
                GraphEditorMode.LiveNoTarget,
                GraphEditorModePolicy.Resolve(true, true, false, false));
        }

        [Test]
        public void AuthoringAndReadOnlyRules()
        {
            Assert.IsTrue(GraphEditorModePolicy.AllowsAuthoring(GraphEditorMode.Author));
            Assert.IsFalse(GraphEditorModePolicy.AllowsAuthoring(GraphEditorMode.Preview));
            Assert.IsFalse(GraphEditorModePolicy.AllowsAuthoring(GraphEditorMode.Live));

            Assert.IsFalse(GraphEditorModePolicy.IsReadOnlyGraph(GraphEditorMode.Author));
            Assert.IsTrue(GraphEditorModePolicy.IsReadOnlyGraph(GraphEditorMode.Preview));
            Assert.IsTrue(GraphEditorModePolicy.IsReadOnlyGraph(GraphEditorMode.Live));
            Assert.IsTrue(GraphEditorModePolicy.IsReadOnlyGraph(GraphEditorMode.LiveNoTarget));

            Assert.IsTrue(GraphEditorModePolicy.AllowsLiveValues(GraphEditorMode.Preview));
            Assert.IsTrue(GraphEditorModePolicy.AllowsLiveValues(GraphEditorMode.Live));
            Assert.IsFalse(GraphEditorModePolicy.AllowsLiveValues(GraphEditorMode.Author));
            Assert.IsFalse(GraphEditorModePolicy.AllowsLiveValues(GraphEditorMode.LiveNoTarget));
        }

        [Test]
        public void TransportAndTargetRules()
        {
            Assert.IsTrue(GraphEditorModePolicy.CanStartPreview(GraphEditorMode.Author));
            Assert.IsTrue(GraphEditorModePolicy.CanStopPreview(GraphEditorMode.Preview));
            Assert.IsTrue(GraphEditorModePolicy.CanPause(GraphEditorMode.Preview));
            Assert.IsTrue(GraphEditorModePolicy.CanPause(GraphEditorMode.Live));
            Assert.IsTrue(GraphEditorModePolicy.CanResetPreview(GraphEditorMode.Preview));
            Assert.IsFalse(GraphEditorModePolicy.CanResetPreview(GraphEditorMode.Live));
            Assert.IsTrue(GraphEditorModePolicy.CanSelectTarget(GraphEditorMode.Live));
            Assert.IsTrue(GraphEditorModePolicy.CanSelectTarget(GraphEditorMode.LiveNoTarget));
            Assert.IsFalse(GraphEditorModePolicy.CanSelectTarget(GraphEditorMode.Author));
            Assert.IsTrue(GraphEditorModePolicy.AllowsDocumentFollow(GraphEditorMode.Author));
            Assert.IsFalse(GraphEditorModePolicy.AllowsDocumentFollow(GraphEditorMode.Preview));
        }

        [Test]
        public void ParametersModeFollowsRuntimeAvailability()
        {
            Assert.AreEqual(
                GraphEditorParametersMode.Empty,
                GraphEditorModePolicy.GetParametersMode(GraphEditorMode.NoDocument));
            Assert.AreEqual(
                GraphEditorParametersMode.Defaults,
                GraphEditorModePolicy.GetParametersMode(GraphEditorMode.Author));
            Assert.AreEqual(
                GraphEditorParametersMode.Live,
                GraphEditorModePolicy.GetParametersMode(GraphEditorMode.Preview));
            Assert.AreEqual(
                GraphEditorParametersMode.Live,
                GraphEditorModePolicy.GetParametersMode(GraphEditorMode.Live));
            Assert.AreEqual(
                GraphEditorParametersMode.ReadOnlyDefaults,
                GraphEditorModePolicy.GetParametersMode(GraphEditorMode.LiveNoTarget));
        }
    }
}
