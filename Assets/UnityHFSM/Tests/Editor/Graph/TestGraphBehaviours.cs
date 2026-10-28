using NUnit.Framework;
using UnityEngine;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphBehaviours
    {
        [SetUp]
        public void SetUp()
        {
            GraphTestFactory.ResetBehaviourLog();
        }

        [Test]
        public void DispatchesEnterLogicAndExitInOrder()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();

            try
            {
                GraphModel model = GraphTestFactory.CreateBehaviourState(
                    behaviour,
                    out StateDef a,
                    out _,
                    out ParameterDef go);

                using (var runtime = new GraphRuntime(model))
                {
                    runtime.Start();
                    Assert.Contains("enter:A", RecordingBehaviour.Events);

                    runtime.Tick(0.1f);
                    Assert.Contains("logic:A", RecordingBehaviour.Events);

                    runtime.SetBool(go.name, true);
                    runtime.Tick(0.1f);
                    Assert.Contains("exit:A", RecordingBehaviour.Events);
                }
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        [Test]
        public void CanExitGatesNeedsExitTime()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();

            try
            {
                GraphModel model = GraphTestFactory.CreateBehaviourState(
                    behaviour,
                    out StateDef a,
                    out StateDef b,
                    out ParameterDef go);

                RecordingBehaviour.AllowExit = false;

                using (var runtime = new GraphRuntime(model))
                {
                    runtime.Start();
                    runtime.SetBool(go.name, true);
                    runtime.Tick(0.1f);
                    Assert.AreEqual(a.id, runtime.GetActiveStateIds()[0]);

                    RecordingBehaviour.AllowExit = true;
                    runtime.Tick(0.1f);
                    Assert.AreEqual(b.id, runtime.GetActiveStateIds()[0]);
                }
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        [Test]
        public void ClonesBehavioursPerRuntimeAndReleasesThem()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();
            var factory = new TrackingBehaviourFactory();

            try
            {
                GraphModel model = GraphTestFactory.CreateBehaviourState(
                    behaviour,
                    out StateDef a,
                    out _,
                    out _);

                var options = new GraphRuntimeOptions { BehaviourFactory = factory };
                var runtime1 = new GraphRuntime(model, options);
                var runtime2 = new GraphRuntime(model, options);

                StateBehaviour clone1 = runtime1.RootMachine.GetState(a.id).Behaviours[0];
                StateBehaviour clone2 = runtime2.RootMachine.GetState(a.id).Behaviours[0];

                Assert.AreNotSame(behaviour, clone1);
                Assert.AreNotSame(clone1, clone2);
                Assert.AreEqual(2, factory.Created);

                runtime1.Dispose();
                runtime2.Dispose();
                Assert.AreEqual(2, factory.Released);
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        [Test]
        public void SubMachineBehavioursReceiveMachineCallbacks()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();

            try
            {
                GraphModel model = GraphTestFactory.CreateNested(
                    out MachineDef root,
                    out StateDef host,
                    out _,
                    out _,
                    out _,
                    out ParameterDef leave);

                host.behaviours.Add(behaviour);

                using (var runtime = new GraphRuntime(model))
                {
                    runtime.Start();
                    Assert.Contains("machine-enter:Sub", RecordingBehaviour.Events);

                    runtime.SetBool(leave.name, true);
                    runtime.Tick(0.1f);
                    Assert.Contains("machine-exit:Sub", RecordingBehaviour.Events);
                }
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }
    }
}
