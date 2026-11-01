using System.Collections.Generic;
using NUnit.Framework;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphRuntimeRegistry
    {
        [Test]
        public void RegistersOnConstructionAndUnregistersOnDispose()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            var live = new List<GraphRuntime>();
            var runtime = new GraphRuntime(model);

            try
            {
                GraphRuntimeRegistry.GetLiveRuntimes(live);
                CollectionAssert.Contains(live, runtime);
            }
            finally
            {
                runtime.Dispose();
            }

            live.Clear();
            GraphRuntimeRegistry.GetLiveRuntimes(live);
            CollectionAssert.DoesNotContain(live, runtime);
        }

        [Test]
        public void DisposedEntryIsPrunedWhenRead()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            var runtime = new GraphRuntime(model);
            runtime.Dispose();

            var live = new List<GraphRuntime>();
            GraphRuntimeRegistry.GetLiveRuntimes(live);

            CollectionAssert.DoesNotContain(live, runtime);
        }

        [Test]
        public void LiveSessionAutoSelectsOnlyTheMatchingGraph()
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

            using (var first = new GraphRuntime(firstModel))
            using (var second = new GraphRuntime(secondModel))
            using (var live = new LiveRuntimeSession())
            {
                live.SetGraphId(firstModel.graphId);

                Assert.AreEqual(1, live.Targets.Count);
                Assert.AreSame(first, live.Targets[0].Runtime);
                Assert.AreSame(first, live.CurrentRuntime);

                for (int i = 0; i < live.Targets.Count; i++)
                    Assert.AreNotSame(second, live.Targets[i].Runtime);
            }
        }
    }
}
