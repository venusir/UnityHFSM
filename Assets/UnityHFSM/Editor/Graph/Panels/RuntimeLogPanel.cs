using System.Text;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Debug details that used to live in the permanent Runtime panel: preview
    /// host, status, active path and a clickable transition log.
    /// </summary>
    public sealed class RuntimeLogPanel : VisualElement
    {
        private readonly StateMachineGraphWindow window;
        private readonly ObjectField hostField;
        private readonly Label statusLabel;
        private readonly Label pathLabel;
        private readonly ScrollView logScroll;
        private readonly VisualElement logList;

        private GraphRuntime lastRuntime;
        private int lastSequence = -1;
        private int lastCount = -1;
        private bool updatingHost;

        public RuntimeLogPanel(StateMachineGraphWindow window)
        {
            this.window = window;

            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 8f;
            body.style.paddingRight = 8f;
            body.style.paddingTop = 4f;
            Add(body);

            var hostRow = new VisualElement();
            hostRow.style.flexDirection = FlexDirection.Row;
            hostRow.style.alignItems = Align.Center;
            body.Add(hostRow);

            hostField = new ObjectField("Preview Host")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
            };
            hostField.style.flexGrow = 1f;
            hostField.style.maxWidth = 320f;
            hostField.tooltip = "Optional GameObject passed to StateBehaviourContext.Host during preview.";
            hostField.RegisterValueChangedCallback(evt =>
            {
                if (updatingHost)
                    return;

                window.SetPreviewHost(evt.newValue as GameObject);
            });
            hostRow.Add(hostField);

            statusLabel = new Label("No runtime bound.");
            statusLabel.style.marginTop = 4f;
            body.Add(statusLabel);

            pathLabel = new Label(string.Empty);
            pathLabel.style.whiteSpace = WhiteSpace.Normal;
            pathLabel.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            body.Add(pathLabel);

            logScroll = new ScrollView();
            logScroll.style.flexGrow = 1f;
            logScroll.style.marginTop = 4f;
            body.Add(logScroll);

            logList = new VisualElement();
            logList.style.flexDirection = FlexDirection.Column;
            logScroll.Add(logList);
        }

        public void UpdateLive(GraphRuntimeSnapshot snapshot, GraphRuntime runtime)
        {
            UpdateHostField(runtime);

            if (runtime == null)
            {
                statusLabel.text = Application.isPlaying
                    ? "No live runtime for this graph. Create a GraphRuntime in code; it is discovered automatically."
                    : "No runtime. Start the preview from the toolbar.";
                pathLabel.text = string.Empty;
                ClearLog();
                lastRuntime = null;
                return;
            }

            if (snapshot == null)
            {
                statusLabel.text = "Runtime is not started or was disposed.";
                pathLabel.text = string.Empty;
                ClearLog();
                lastRuntime = null;
                return;
            }

            statusLabel.text =
                (snapshot.isStarted ? "Started" : "Not started")
                + (snapshot.isPaused ? " / Paused" : string.Empty)
                + " / t=" + snapshot.time.ToString("0.00")
                + (window.IsPreviewing ? " / Preview" : " / Live");

            if (!string.IsNullOrEmpty(window.PreviewLastError))
                statusLabel.text += "\nError: " + window.PreviewLastError;

            pathLabel.text = "Active: " + window.BuildActivePath(snapshot);

            int sequence = snapshot.transitions.Count > 0
                ? snapshot.transitions[snapshot.transitions.Count - 1].sequence
                : -1;
            int count = snapshot.transitions.Count;

            if (!ReferenceEquals(lastRuntime, runtime) || sequence != lastSequence || count != lastCount)
            {
                lastRuntime = runtime;
                lastSequence = sequence;
                lastCount = count;
                RebuildLog(snapshot);
            }
        }

        public void ResetView()
        {
            statusLabel.text = "No runtime bound.";
            pathLabel.text = string.Empty;
            ClearLog();
            lastRuntime = null;
        }

        private void UpdateHostField(GraphRuntime runtime)
        {
            GameObject host = null;
            bool editable = false;

            if (window.IsPreviewing || !Application.isPlaying)
            {
                host = window.PreviewHost;
                editable = true;
            }
            else if (runtime != null)
            {
                host = runtime.Options.Host;
                editable = false;
            }

            updatingHost = true;
            if (hostField.value != host)
                hostField.SetValueWithoutNotify(host);
            updatingHost = false;

            hostField.SetEnabled(editable);
            hostField.tooltip = editable
                ? "Optional GameObject passed to StateBehaviourContext.Host during preview."
                : "The live runtime owns its host; change it in code.";
        }

        private void RebuildLog(GraphRuntimeSnapshot snapshot)
        {
            logList.Clear();

            int count = snapshot.transitions.Count;
            if (count == 0)
            {
                var empty = new Label("Transition log: (empty)");
                empty.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
                empty.style.fontSize = 11f;
                logList.Add(empty);
                return;
            }

            int start = Mathf.Max(0, count - 50);
            VisualElement lastRow = null;

            for (int i = start; i < count; i++)
            {
                TransitionRecord record = snapshot.transitions[i];
                if (record == null)
                    continue;

                var row = new Button(() => window.NavigateToTransition(record))
                {
                    text = BuildRecordText(record),
                };
                row.tooltip = "Click to locate this transition.";
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.unityTextAlign = TextAnchor.UpperLeft;
                row.style.height = new StyleLength(StyleKeyword.Auto);
                row.style.minHeight = 0f;
                row.style.marginLeft = 0f;
                row.style.marginRight = 0f;
                row.style.marginTop = 0f;
                row.style.marginBottom = 1f;
                row.style.paddingLeft = 2f;
                row.style.paddingRight = 2f;
                row.style.paddingTop = 1f;
                row.style.paddingBottom = 1f;
                row.style.backgroundColor = new StyleColor(Color.clear);
                row.style.borderLeftWidth = 0f;
                row.style.borderRightWidth = 0f;
                row.style.borderTopWidth = 0f;
                row.style.borderBottomWidth = 0f;
                row.style.fontSize = 11f;
                row.style.color = new StyleColor(
                    record.isExit ? GraphEditorStyles.ErrorColor : GraphEditorStyles.MutedTextColor);

                logList.Add(row);
                lastRow = row;
            }

            if (lastRow != null)
            {
                VisualElement target = lastRow;
                schedule.Execute(() => logScroll.ScrollTo(target));
            }
        }

        private void ClearLog()
        {
            logList.Clear();
            lastSequence = -1;
            lastCount = -1;
        }

        private static string BuildRecordText(TransitionRecord record)
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
