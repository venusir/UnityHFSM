using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class StateGraphNode : Node
    {
        public string StateId { get; }
        public string MachineId { get; }
        public bool IsSubMachine { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        public StateGraphNode(
            string stateId,
            string machineId,
            string stateName,
            bool isSubMachine,
            Action<string> onOpenSubMachine,
            StateMachineGraphView view)
        {
            StateId = stateId;
            MachineId = machineId;
            IsSubMachine = isSubMachine;

            title = stateName;
            tooltip = isSubMachine
                ? "Sub-state machine. Double-click to open."
                : "State";

            capabilities = Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable;
            AddToClassList("unityhfsm-state-node");
            style.minWidth = 170f;
            style.backgroundColor = new StyleColor(
                isSubMachine
                    ? new Color(0.20f, 0.25f, 0.32f)
                    : new Color(0.18f, 0.18f, 0.18f));

            InputPort = Port.Create<TransitionEdge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            InputPort.portName = "In";
            InputPort.AddManipulator(new EdgeConnector<TransitionEdge>(view.EdgeConnectorListener));
            inputContainer.Add(InputPort);

            OutputPort = Port.Create<TransitionEdge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(bool));
            OutputPort.portName = "Out";
            OutputPort.AddManipulator(new EdgeConnector<TransitionEdge>(view.EdgeConnectorListener));
            outputContainer.Add(OutputPort);

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2 && IsSubMachine)
                {
                    onOpenSubMachine?.Invoke(StateId);
                    evt.StopPropagation();
                }
            });
        }
    }
}
