using System;
using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphRuntimeBasic
    {
        [Test]
        public void StartsInStartStateAndEvaluatesConditions()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out StateDef idle,
                out StateDef move,
                out ParameterDef go,
                out ParameterDef speed);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();

                Assert.AreEqual(idle.id, runtime.GetActiveStateIds()[0]);
                Assert.AreEqual(0f, runtime.GetFloat(speed.name));

                runtime.SetBool(go.name, true);
                runtime.Tick(0.1f);
                Assert.AreEqual(move.id, runtime.GetActiveStateIds()[0]);

                runtime.SetBool(go.name, false);
                runtime.SetFloat(speed.name, 2f);
                runtime.Tick(0.1f);
                Assert.AreEqual(idle.id, runtime.GetActiveStateIds()[0]);
            }
        }

        [Test]
        public void RejectsNegativeDelta()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Tick(-1f));
            }
        }

        [Test]
        public void SnapshotDoesNotThrowWithoutPendingTransition()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out StateDef idle,
                out _,
                out _,
                out _);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                GraphRuntimeSnapshot snapshot = runtime.GetSnapshot();

                Assert.AreEqual(1, snapshot.activePathStateIds.Count);
                Assert.AreEqual(idle.id, snapshot.activePathStateIds[0]);
                Assert.AreEqual(1, snapshot.machines.Count);
                Assert.IsFalse(snapshot.machines[0].hasPendingTransition);
            }
        }
    }
}
