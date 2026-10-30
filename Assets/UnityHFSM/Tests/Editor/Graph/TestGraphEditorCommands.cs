using NUnit.Framework;
using UnityHFSM.Graph.Editor;
using UnityEngine;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphEditorCommands
    {
        private StateMachineGraphAsset asset;
        private GraphEditorController controller;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            asset.SetGraph(GraphModelFactory.CreateNew());
            controller = new GraphEditorController(asset);
        }

        [TearDown]
        public void TearDown()
        {
            if (asset != null)
                Object.DestroyImmediate(asset);
        }

        [Test]
        public void AddsAndDeletesStatesAndTransitions()
        {
            string rootId = asset.Graph.rootMachineId;
            string firstStateId = asset.Graph.states[0].id;
            string secondStateId = controller.AddState(rootId, new Vector2(100f, 100f));

            Assert.AreEqual(2, asset.Graph.states.Count);

            string transitionId = controller.AddTransition(
                rootId,
                TransitionSourceKind.State,
                firstStateId,
                TransitionTargetKind.State,
                secondStateId);

            Assert.IsNotNull(transitionId);
            Assert.AreEqual(1, asset.Graph.transitions.Count);

            controller.DeleteState(secondStateId);
            Assert.AreEqual(1, asset.Graph.states.Count);
            Assert.AreEqual(0, asset.Graph.transitions.Count);
        }

        [Test]
        public void DeletingSubMachineHostCascadesToChildMachine()
        {
            string rootId = asset.Graph.rootMachineId;
            string hostId = controller.AddSubMachine(rootId, new Vector2(100f, 100f));
            StateDef host = controller.FindState(hostId);

            Assert.IsNotNull(host);
            Assert.AreEqual(StateKind.HybridSubMachine, host.kind);

            MachineDef child = controller.FindMachine(host.subMachineId);
            Assert.IsNotNull(child);
            Assert.AreEqual(1, CountStates(child.id));

            controller.DeleteState(hostId);
            Assert.IsNull(controller.FindState(hostId));
            Assert.IsNull(controller.FindMachine(child.id));
            Assert.AreEqual(0, CountStates(child.id));
        }

        [Test]
        public void DeletingParameterRemovesConditions()
        {
            string rootId = asset.Graph.rootMachineId;
            string firstStateId = asset.Graph.states[0].id;
            string secondStateId = controller.AddState(rootId, new Vector2(100f, 100f));

            controller.AddParameter();
            ParameterDef parameter = asset.Graph.parameters[0];

            string transitionId = controller.AddTransition(
                rootId,
                TransitionSourceKind.State,
                firstStateId,
                TransitionTargetKind.State,
                secondStateId);

            controller.AddCondition(transitionId);
            Assert.AreEqual(1, controller.FindTransition(transitionId).conditions.Count);

            controller.DeleteParameter(parameter.id);
            Assert.AreEqual(0, asset.Graph.parameters.Count);
            Assert.AreEqual(0, controller.FindTransition(transitionId).conditions.Count);
        }

        [Test]
        public void DuplicateStateNameIsRejected()
        {
            string rootId = asset.Graph.rootMachineId;
            string firstStateName = asset.Graph.states[0].name;
            string secondStateId = controller.AddState(rootId, Vector2.zero);

            controller.RenameState(secondStateId, firstStateName);

            Assert.AreEqual("New State", controller.FindState(secondStateId).name);
        }

        [Test]
        public void ChangingParameterTypeFixesConditionComparison()
        {
            string rootId = asset.Graph.rootMachineId;
            string firstStateId = asset.Graph.states[0].id;
            string secondStateId = controller.AddState(rootId, Vector2.zero);

            controller.AddParameter();
            ParameterDef parameter = asset.Graph.parameters[0];

            string transitionId = controller.AddTransition(
                rootId,
                TransitionSourceKind.State,
                firstStateId,
                TransitionTargetKind.State,
                secondStateId);

            controller.AddCondition(transitionId);
            controller.SetParameterType(parameter.id, BlackboardParameterType.Bool);

            Assert.AreEqual(
                ConditionComparison.IsTrue,
                controller.FindTransition(transitionId).conditions[0].comparison);
        }

        [Test]
        public void AddParameterWithTypeCreatesRequestedType()
        {
            string parameterId = controller.AddParameter(BlackboardParameterType.Trigger);

            Assert.IsFalse(string.IsNullOrEmpty(parameterId));
            Assert.AreEqual(1, asset.Graph.parameters.Count);

            ParameterDef parameter = asset.Graph.parameters[0];
            Assert.AreEqual(parameterId, parameter.id);
            Assert.AreEqual(BlackboardParameterType.Trigger, parameter.type);
            Assert.AreEqual("Parameter", parameter.name);
            Assert.IsFalse(parameter.defaultBool);
        }

        [Test]
        public void AddParameterWithoutTypeStillDefaultsToFloat()
        {
            controller.AddParameter();

            Assert.AreEqual(1, asset.Graph.parameters.Count);
            Assert.AreEqual(BlackboardParameterType.Float, asset.Graph.parameters[0].type);
        }

        private int CountStates(string machineId)
        {
            int count = 0;
            for (int i = 0; i < asset.Graph.states.Count; i++)
            {
                StateDef state = asset.Graph.states[i];
                if (state != null && state.machineId == machineId)
                    count++;
            }

            return count;
        }
    }
}
