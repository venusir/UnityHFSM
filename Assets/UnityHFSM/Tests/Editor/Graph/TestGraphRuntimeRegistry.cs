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
        public void SessionAutoSelectsOnlyTheMatchingGraph()
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
            using (var session = new RuntimeSession(null))
            {
                session.SetGraphId(firstModel.graphId);
                session.RefreshLiveTargets();

                Assert.GreaterOrEqual(session.Targets.Count, 2);
                Assert.IsNotNull(session.SelectedTarget);
                Assert.AreSame(first, session.SelectedTarget.Runtime);
                Assert.AreSame(first, session.CurrentRuntime);

                int secondIndex = -1;
                for (int i = 0; i < session.Targets.Count; i++)
                {
                    if (ReferenceEquals(session.Targets[i].Runtime, second))
                    {
                        secondIndex = i;
                        break;
                    }
                }

                Assert.GreaterOrEqual(secondIndex, 0);
                session.SelectTarget(secondIndex);
                Assert.AreSame(second, session.CurrentRuntime);
            }
        }
    }
}
