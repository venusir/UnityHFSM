using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphSnapshotAndRebuild
    {
        [Test]
        public void SnapshotTracksNestedActivePathAndTransitionLog()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out MachineDef root,
                out StateDef host,
                out _,
                out StateDef childA,
                out StateDef done,
                out ParameterDef leave);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                GraphRuntimeSnapshot before = runtime.GetSnapshot();

                Assert.AreEqual(2, before.activePathMachineIds.Count);
                Assert.AreEqual(2, before.activePathStateIds.Count);
                Assert.AreEqual(host.id, before.activePathStateIds[0]);
                Assert.AreEqual(childA.id, before.activePathStateIds[1]);
                Assert.AreEqual(0, before.transitions.Count);

                runtime.SetBool(leave.name, true);
                runtime.Tick(0.1f);

                GraphRuntimeSnapshot after = runtime.GetSnapshot();
                Assert.AreEqual(done.id, after.activePathStateIds[0]);
                Assert.IsTrue(after.transitions.Exists(record => record.toStateId == done.id));
                Assert.IsFalse(after.machines[0].hasPendingTransition);
            }
        }

        [Test]
        public void PauseBlocksTicksAndStepAdvancesOnce()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out StateDef move,
                out ParameterDef go,
                out _);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetBool(go.name, true);
                runtime.Pause();
                runtime.Tick(0.1f);
                Assert.AreNotEqual(move.id, runtime.GetActiveStateIds()[0]);

                runtime.Step(0.1f);
                Assert.AreEqual(move.id, runtime.GetActiveStateIds()[0]);
            }
        }

        [Test]
        public void BlackboardValuesCanBeCopiedBetweenRuntimes()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out ParameterDef speed);

            using (var first = new GraphRuntime(model))
            using (var second = new GraphRuntime(model))
            {
                first.SetFloat(speed.name, 3f);
                second.Blackboard.CopyValuesFrom(first.Blackboard);
                Assert.AreEqual(3f, second.GetFloat(speed.name));
            }
        }
    }
}
