using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphDelays
    {
        [Test]
        public void DirectDelayWaitsForElapsedStateTime()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out MachineDef root);
            StateDef a = GraphTestFactory.AddState(model, root.id, "A");
            StateDef b = GraphTestFactory.AddState(model, root.id, "B");
            root.startStateId = a.id;

            ParameterDef go = GraphTestFactory.AddParameter(model, "Go", BlackboardParameterType.Bool);
            GraphTestFactory.AddTransition(
                model,
                root.id,
                TransitionSourceKind.State,
                a.id,
                TransitionTargetKind.State,
                b.id,
                0.5f,
                false,
                0,
                GraphTestFactory.Condition(go, ConditionComparison.IsTrue));

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetBool(go.name, true);

                runtime.Tick(0.4f);
                Assert.AreEqual(a.id, runtime.GetActiveStateIds()[0]);

                runtime.Tick(0.1f);
                Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);
            }
        }

        [Test]
        public void AnyStateDelayResetsAfterInternalStateChange()
        {
            GraphModel model = GraphTestFactory.CreateAnyDelay(
                out StateDef a,
                out StateDef b,
                out StateDef c,
                out ParameterDef goB);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();

                runtime.Tick(0.5f);
                runtime.SetBool(goB.name, true);
                runtime.Tick(0.1f);
                Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);

                // The Any-State delay must restart when B is entered.
                runtime.Tick(0.5f);
                Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);

                runtime.Tick(0.5f);
                Assert.AreEqual(c.id, runtime.GetActiveStateIds()[0]);
            }
        }

        [Test]
        public void ZeroDelayFiresOnFirstTick()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out MachineDef root);
            StateDef a = GraphTestFactory.AddState(model, root.id, "A");
            StateDef b = GraphTestFactory.AddState(model, root.id, "B");
            root.startStateId = a.id;

            ParameterDef go = GraphTestFactory.AddParameter(model, "Go", BlackboardParameterType.Bool);
            GraphTestFactory.AddTransition(
                model,
                root.id,
                TransitionSourceKind.State,
                a.id,
                TransitionTargetKind.State,
                b.id,
                0f,
                false,
                0,
                GraphTestFactory.Condition(go, ConditionComparison.IsTrue));

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetBool(go.name, true);
                runtime.Tick(0f);
                Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);
            }
        }
    }
}
