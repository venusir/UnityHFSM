using NUnit.Framework;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestLiveRuntimeSession
    {
        [Test]
        public void OnlyMatchingLiveRuntimesAreOfferedAndPreviewsAreExcluded()
        {
            GraphModel firstModel = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);
            GraphModel secondModel = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            using (var preview = new GraphPreviewSession(firstModel))
            using (var live = new LiveRuntimeSession())
            using (var firstRuntime = new GraphRuntime(firstModel))
            using (var secondRuntime = new GraphRuntime(secondModel))
            {
                Assert.IsTrue(preview.Start());
                live.SetGraphId(firstModel.graphId);

                Assert.AreEqual(1, live.Targets.Count);
                Assert.AreSame(firstRuntime, live.Targets[0].Runtime);

                foreach (LiveRuntimeSession.Target target in live.Targets)
                    Assert.AreNotSame(preview.Runtime, target.Runtime);

                live.SelectTarget(0);
                Assert.AreSame(firstRuntime, live.CurrentRuntime);

                live.Poll();
                Assert.IsNotNull(live.Snapshot);
            }
        }

        [Test]
        public void EmptyGraphIdMatchesNothing()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);
            model.graphId = string.Empty;

            using (var live = new LiveRuntimeSession())
            using (var runtime = new GraphRuntime(model))
            {
                live.SetGraphId(model.graphId);
                Assert.AreEqual(0, live.Targets.Count);
                Assert.IsNull(live.CurrentRuntime);
            }
        }
    }
}
