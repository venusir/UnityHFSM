using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public enum PseudoNodeKind
    {
        Entry = 0,
        AnyState = 1,
        Exit = 2,
    }

    public sealed class PseudoGraphNode : Node
    {
        public PseudoNodeKind Kind { get; }
        public string MachineId { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }
        public bool ExitEnabled { get; }

        public PseudoGraphNode(
            PseudoNodeKind kind,
            string machineId,
            StateMachineGraphView view,
            bool exitEnabled)
        {
            Kind = kind;
            MachineId = machineId;
            ExitEnabled = exitEnabled;

            capabilities = Capabilities.Selectable | Capabilities.Movable;
            AddToClassList("unityhfsm-pseudo-node");
            style.minWidth = 110f;

            switch (kind)
            {
                case PseudoNodeKind.Entry:
                    title = "Entry";
                    tooltip = "Connects to the machine's start state.";
                    style.backgroundColor = new StyleColor(new Color(0.18f, 0.28f, 0.18f));
                    OutputPort = CreateOutputPort(view);
                    outputContainer.Add(OutputPort);
                    break;

                case PseudoNodeKind.AnyState:
                    title = "Any State";
                    tooltip = "Transitions evaluated from every state in this machine.";
                    style.backgroundColor = new StyleColor(new Color(0.30f, 0.22f, 0.14f));
                    OutputPort = CreateOutputPort(view);
                    outputContainer.Add(OutputPort);
                    break;

                case PseudoNodeKind.Exit:
                    title = "Exit";
                    tooltip = exitEnabled
                        ? "Exit the current sub-state machine."
                        : "The root machine cannot be exited.";
                    style.backgroundColor = new StyleColor(new Color(0.30f, 0.16f, 0.16f));
                    InputPort = CreateInputPort(view);
                    inputContainer.Add(InputPort);

                    if (!exitEnabled)
                        pickingMode = PickingMode.Ignore;
                    break;
            }
        }

        private static Port CreateInputPort(StateMachineGraphView view)
        {
            var port = Port.Create<TransitionEdge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(bool));
            port.portName = "In";
            port.AddManipulator(new EdgeConnector<TransitionEdge>(view.EdgeConnectorListener));
            return port;
        }

        private static Port CreateOutputPort(StateMachineGraphView view)
        {
            var port = Port.Create<TransitionEdge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,
                typeof(bool));
            port.portName = "Out";
            port.AddManipulator(new EdgeConnector<TransitionEdge>(view.EdgeConnectorListener));
            return port;
        }
    }
}
