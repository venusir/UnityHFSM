using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphTriggers
    {
        [Test]
        public void TriggerIsConsumedOnlyWhenAllConditionsPass()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out MachineDef root);
            StateDef a = GraphTestFactory.AddState(model, root.id, "A");
            StateDef b = GraphTestFactory.AddState(model, root.id, "B");
            root.startStateId = a.id;

            ParameterDef trigger = GraphTestFactory.AddParameter(model, "T", BlackboardParameterType.Trigger);
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
                GraphTestFactory.Condition(trigger, ConditionComparison.IsSet),
                GraphTestFactory.Condition(go, ConditionComparison.IsTrue));

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetTrigger(trigger.name);
                runtime.Tick(0.1f);

                Assert.AreEqual(a.id, runtime.GetActiveStateIds()[0]);
                Assert.IsTrue(runtime.GetTrigger(trigger.name));

                runtime.SetBool(go.name, true);
                runtime.Tick(0.1f);

                Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);
                Assert.IsFalse(runtime.GetTrigger(trigger.name));
            }
        }

        [Test]
        public void MultipleTriggerConditionsAreConsumedTogether()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out MachineDef root);
            StateDef a = GraphTestFactory.AddState(model, root.id, "A");
            StateDef b = GraphTestFactory.AddState(model, root.id, "B");
            root.startStateId = a.id;

            ParameterDef first = GraphTestFactory.AddParameter(model, "First", BlackboardParameterType.Trigger);
            ParameterDef second = GraphTestFactory.AddParameter(model, "Second", BlackboardParameterType.Trigger);

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
                GraphTestFactory.Condition(first, ConditionComparison.IsSet),
                GraphTestFactory.Condition(second, ConditionComparison.IsSet));

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetTrigger(first.name);
                runtime.Tick(0.1f);
                Assert.AreEqual(a.id, runtime.GetActiveStateIds()[0]);

                runtime.SetTrigger(second.name);
                runtime.Tick(0.1f);
                Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);
                Assert.IsFalse(runtime.GetTrigger(first.name));
                Assert.IsFalse(runtime.GetTrigger(second.name));
            }
        }

        [Test]
        public void UnconsumedTriggerRemainsSet()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out MachineDef root);
            StateDef a = GraphTestFactory.AddState(model, root.id, "A");
            root.startStateId = a.id;

            ParameterDef trigger = GraphTestFactory.AddParameter(model, "T", BlackboardParameterType.Trigger);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetTrigger(trigger.name);
                runtime.Tick(0.1f);
                Assert.IsTrue(runtime.GetTrigger(trigger.name));

                runtime.ResetTrigger(trigger.name);
                Assert.IsFalse(runtime.GetTrigger(trigger.name));
            }
        }
    }
}
