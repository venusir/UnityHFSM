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

        private readonly Label badgeLabel;

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

            badgeLabel = new Label();
            badgeLabel.style.fontSize = 9f;
            badgeLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            badgeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            badgeLabel.style.overflow = Overflow.Hidden;
            badgeLabel.style.textOverflow = TextOverflow.Ellipsis;
            badgeLabel.style.marginLeft = 4f;
            badgeLabel.style.marginRight = 4f;
            badgeLabel.style.marginTop = 2f;
            badgeLabel.style.marginBottom = 0f;
            badgeLabel.style.display = DisplayStyle.None;

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

        public override Rect GetPosition()
        {
            // Node.GetPosition reads layout, which is NaN for a view that has not
            // been attached to a panel yet (used by EditMode smoke tests). The
            // style values are the authoritative position written by SetPosition.
            return new Rect(
                style.left.value.value,
                style.top.value.value,
                style.width.value.value,
                style.height.value.value);
        }

        public void SetBadges(string text)
        {
            if (badgeLabel == null)
                return;

            string value = text ?? string.Empty;
            badgeLabel.text = value;
            badgeLabel.tooltip = value;

            if (string.IsNullOrEmpty(value))
            {
                badgeLabel.style.display = DisplayStyle.None;
                return;
            }

            if (badgeLabel.parent == null)
            {
                VisualElement badgeHost = topContainer ?? mainContainer;
                if (badgeHost != null)
                {
                    badgeHost.style.flexDirection = FlexDirection.Row;
                    badgeHost.style.alignItems = Align.Center;
                    badgeHost.Add(badgeLabel);
                }
            }

            badgeLabel.style.display = DisplayStyle.Flex;
        }
    }
}
