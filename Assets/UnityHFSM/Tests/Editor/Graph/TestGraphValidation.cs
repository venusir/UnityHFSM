using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphValidation
    {
        private static void AssertInvalid(GraphModel model)
        {
            Assert.IsTrue(GraphValidator.Validate(model).HasErrors);
            Assert.Throws<GraphBuildException>(() => new GraphRuntime(model));
        }

        [Test]
        public void ValidModelHasNoErrors()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            Assert.IsFalse(GraphValidator.Validate(model).HasErrors);
        }

        [Test]
        public void DuplicateStateNamesAreRejected()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out StateDef idle,
                out StateDef move,
                out _,
                out _);

            move.name = idle.name;
            AssertInvalid(model);
        }

        [Test]
        public void MissingStartStateIsRejected()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            MachineDef root = model.FindMachine(model.rootMachineId);
            root.startStateId = string.Empty;
            AssertInvalid(model);
        }

        [Test]
        public void MissingConditionParameterIsRejected()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out StateDef idle,
                out StateDef move,
                out _,
                out _);

            foreach (TransitionDef transition in model.transitions)
            {
                if (transition.fromStateId == idle.id && transition.toStateId == move.id)
                {
                    transition.conditions[0].parameterId = "missing-parameter";
                    break;
                }
            }

            AssertInvalid(model);
        }

        [Test]
        public void DuplicateOrderInBucketIsRejected()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out StateDef idle,
                out StateDef move,
                out ParameterDef go,
                out _);

            GraphTestFactory.AddTransition(
                model,
                model.rootMachineId,
                TransitionSourceKind.State,
                idle.id,
                TransitionTargetKind.State,
                move.id,
                0f,
                false,
                0,
                GraphTestFactory.Condition(go, ConditionComparison.IsTrue));

            AssertInvalid(model);
        }

        [Test]
        public void CrossMachineTransitionIsRejected()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out MachineDef root,
                out StateDef host,
                out _,
                out StateDef childA,
                out _,
                out _);

            GraphTestFactory.AddTransition(
                model,
                root.id,
                TransitionSourceKind.State,
                host.id,
                TransitionTargetKind.State,
                childA.id);

            AssertInvalid(model);
        }

        [Test]
        public void InvalidNestedMachineKindIsRejected()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out _,
                out _,
                out MachineDef child,
                out _,
                out _,
                out _);

            child.kind = MachineKind.StateMachine;
            AssertInvalid(model);
        }
    }
}
