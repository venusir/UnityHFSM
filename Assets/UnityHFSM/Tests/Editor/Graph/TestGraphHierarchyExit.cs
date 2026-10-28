using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphHierarchyExit
    {
        [Test]
        public void ChildExitTransitionCompletesPendingParentTransition()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out MachineDef root,
                out StateDef host,
                out MachineDef child,
                out StateDef childA,
                out StateDef done,
                out ParameterDef leave);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();

                Assert.AreEqual(2, runtime.GetActiveStateIds().Count);
                Assert.AreEqual(host.id, runtime.GetActiveStateIds()[0]);
                Assert.AreEqual(childA.id, runtime.GetActiveStateIds()[1]);

                runtime.SetBool(leave.name, true);
                runtime.Tick(0.1f);

                Assert.AreEqual(done.id, runtime.GetActiveStateIds()[0]);
            }
        }

        [Test]
        public void RootExitTransitionIsAValidationError()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out StateDef idle,
                out _,
                out _,
                out _);

            GraphTestFactory.AddTransition(
                model,
                model.rootMachineId,
                TransitionSourceKind.State,
                idle.id,
                TransitionTargetKind.Exit,
                string.Empty);

            GraphValidationResult validation = GraphValidator.Validate(model);
            Assert.IsTrue(validation.HasErrors);
            Assert.Throws<GraphBuildException>(() => new GraphRuntime(model));
        }
    }
}
