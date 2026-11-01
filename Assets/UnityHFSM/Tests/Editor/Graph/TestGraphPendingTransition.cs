using NUnit.Framework;
using UnityEngine;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphPendingTransition
    {
        [Test]
        public void SnapshotReportsExactPendingTransitionId()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out MachineDef root);
            StateDef a = GraphTestFactory.AddState(model, root.id, "A", needsExitTime: true);
            StateDef b = GraphTestFactory.AddState(model, root.id, "B");
            root.startStateId = a.id;

            TransitionDef transition = GraphTestFactory.AddTransition(
                model,
                root.id,
                TransitionSourceKind.State,
                a.id,
                TransitionTargetKind.State,
                b.id);

            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();
            a.behaviours.Add(behaviour);
            RecordingBehaviour.AllowExit = false;

            try
            {
                using (var runtime = new GraphRuntime(model))
                {
                    runtime.Start();
                    runtime.Tick(0.01f);

                    GraphRuntimeSnapshot snapshot = runtime.GetSnapshot();
                    MachineRuntimeSnapshot machine = snapshot.machines[0];

                    Assert.IsTrue(machine.hasPendingTransition);
                    Assert.AreEqual(b.id, machine.pendingStateId);
                    Assert.AreEqual(transition.id, machine.pendingTransitionId);

                    RecordingBehaviour.AllowExit = true;
                    runtime.Tick(0.01f);

                    snapshot = runtime.GetSnapshot();
                    machine = snapshot.machines[0];
                    Assert.AreEqual(b.id, machine.activeStateId);
                    Assert.IsFalse(machine.hasPendingTransition);
                    Assert.IsTrue(string.IsNullOrEmpty(machine.pendingTransitionId));
                }
            }
            finally
            {
                RecordingBehaviour.AllowExit = true;
                Object.DestroyImmediate(behaviour);
            }
        }
    }
}
