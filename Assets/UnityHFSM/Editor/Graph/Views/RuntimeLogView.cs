using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>Transition log and runtime errors. No target/path duplication.</summary>
    public sealed class RuntimeLogView : GraphEditorView
    {
        private readonly ScrollView scroll;
        private readonly VisualElement log;
        private readonly Label errorLabel;

        private int lastSequence = -1;
        private int lastCount = -1;
        private GraphRuntime lastRuntime;

        public RuntimeLogView()
        {
            style.flexGrow = 1f;
            style.minHeight = 0f;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 4f;

            errorLabel = new Label();
            errorLabel.style.whiteSpace = WhiteSpace.Normal;
            errorLabel.style.color = new StyleColor(GraphEditorStyles.ErrorColor);
            errorLabel.style.display = DisplayStyle.None;
            Add(errorLabel);

            scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            Add(scroll);

            log = new VisualElement();
            log.style.flexDirection = FlexDirection.Column;
            scroll.Add(log);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            string error = Session.PreviewLastError;
            errorLabel.text = error ?? string.Empty;
            errorLabel.style.display = string.IsNullOrEmpty(error)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            if ((change & (GraphEditorChange.Document
                | GraphEditorChange.Mode
                | GraphEditorChange.Targets
                | GraphEditorChange.All)) != 0)
            {
                lastRuntime = null;
                lastSequence = -1;
                lastCount = -1;
            }

            if ((change & (GraphEditorChange.Runtime
                | GraphEditorChange.Document
                | GraphEditorChange.Mode
                | GraphEditorChange.Targets
                | GraphEditorChange.All)) != 0)
            {
                UpdateLog();
            }
        }

        private void UpdateLog()
        {
            GraphRuntime runtime = Session.CurrentRuntime;
            GraphRuntimeSnapshot snapshot = Session.Snapshot;

            if (runtime == null || snapshot == null)
            {
                lastRuntime = runtime;
                lastSequence = -1;
                lastCount = -1;
                log.Clear();
                log.Add(GraphEditorStyles.CreateMutedLabel(
                    Session.Mode == GraphEditorMode.LiveNoTarget
                        ? "No matching live runtime."
                        : "No runtime. Start Preview or enter Play Mode with a GraphRuntime."));
                return;
            }

            int sequence = snapshot.transitions.Count > 0
                ? snapshot.transitions[snapshot.transitions.Count - 1].sequence
                : -1;
            int count = snapshot.transitions.Count;

            if (ReferenceEquals(runtime, lastRuntime)
                && sequence == lastSequence
                && count == lastCount)
            {
                return;
            }

            lastRuntime = runtime;
            lastSequence = sequence;
            lastCount = count;
            Rebuild(snapshot);
        }

        private void Rebuild(GraphRuntimeSnapshot snapshot)
        {
            log.Clear();

            if (snapshot.transitions.Count == 0)
            {
                log.Add(GraphEditorStyles.CreateMutedLabel("Transition log: (empty)"));
                return;
            }

            int start = Mathf.Max(0, snapshot.transitions.Count - 50);
            VisualElement last = null;

            for (int i = start; i < snapshot.transitions.Count; i++)
            {
                TransitionRecord record = snapshot.transitions[i];
                if (record == null)
                    continue;

                var button = new Button(() => Commands.Diagnostics.OpenTransitionRecord(record))
                {
                    text = BuildText(record),
                };
                button.tooltip = "Click to locate this transition.";
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.unityTextAlign = TextAnchor.UpperLeft;
                button.style.height = new StyleLength(StyleKeyword.Auto);
                button.style.minHeight = 0f;
                button.style.marginLeft = 0f;
                button.style.marginRight = 0f;
                button.style.marginTop = 0f;
                button.style.marginBottom = 1f;
                button.style.paddingLeft = 2f;
                button.style.paddingRight = 2f;
                button.style.paddingTop = 1f;
                button.style.paddingBottom = 1f;
                button.style.backgroundColor = new StyleColor(Color.clear);
                button.style.borderLeftWidth = 0f;
                button.style.borderRightWidth = 0f;
                button.style.borderTopWidth = 0f;
                button.style.borderBottomWidth = 0f;
                button.style.fontSize = GraphEditorMetrics.FontSize;
                button.style.color = new StyleColor(
                    record.isExit ? GraphEditorStyles.ErrorColor : GraphEditorStyles.MutedTextColor);

                log.Add(button);
                last = button;
            }

            if (last != null)
            {
                VisualElement target = last;
                schedule.Execute(() => scroll.ScrollTo(target));
            }
        }

        private static string BuildText(TransitionRecord record)
        {
            var builder = new StringBuilder();
            builder.Append('#').Append(record.sequence)
                .Append(" t=").Append(record.time.ToString("0.00"))
                .Append(' ').Append(record.machineName)
                .Append(": ")
                .Append(string.IsNullOrEmpty(record.fromStateName) ? "(Any)" : record.fromStateName)
                .Append(" -> ")
                .Append(record.isExit
                    ? "Exit"
                    : string.IsNullOrEmpty(record.toStateName) ? "(none)" : record.toStateName);
            return builder.ToString();
        }
    }
}
