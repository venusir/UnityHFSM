using NUnit.Framework;
using UnityHFSM;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphCoexistence
    {
        [Test]
        public void CodeFirstStateMachineStillWorksAlongsideGraphRuntime()
        {
            bool enteredA = false;
            var fsm = new StateMachine();
            fsm.AddState("A", onEnter: _ => enteredA = true);
            fsm.AddState("B");
            fsm.AddTransition("A", "B", _ => true);
            fsm.SetStartState("A");
            fsm.Init();
            fsm.OnLogic();

            Assert.IsTrue(enteredA);
            Assert.AreEqual("B", fsm.ActiveStateName);

            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out StateDef move,
                out ParameterDef go,
                out _);

            using (var runtime = new GraphRuntime(model))
            {
                runtime.Start();
                runtime.SetBool(go.name, true);
                runtime.Tick(0.1f);
                Assert.AreEqual(move.id, runtime.GetActiveStateIds()[0]);
            }

            Assert.AreEqual("B", fsm.ActiveStateName);
        }
    }
}
