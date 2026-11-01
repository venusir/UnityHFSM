using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Bottom status bar: current mode, transient status message and the
    /// Problems/Runtime entry for the Diagnostics drawer.
    /// </summary>
    public sealed class GraphStatusBar : GraphEditorView
    {
        private readonly Label modePill;
        private readonly Label statusLabel;
        private readonly Button diagnosticsButton;

        public GraphStatusBar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = GraphEditorMetrics.StatusBarHeight;
            style.minHeight = GraphEditorMetrics.StatusBarHeight;
            style.maxHeight = GraphEditorMetrics.StatusBarHeight;
            style.flexShrink = 0f;
            style.paddingLeft = GraphEditorMetrics.Spacing;
            style.paddingRight = GraphEditorMetrics.Spacing;
            style.backgroundColor = new StyleColor(GraphEditorStyles.HeaderBackgroundColor);
            style.borderTopWidth = 1f;
            style.borderTopColor = new StyleColor(GraphEditorStyles.SeparatorColor);

            modePill = new Label("No Graph");
            modePill.style.height = 18f;
            modePill.style.paddingLeft = 8f;
            modePill.style.paddingRight = 8f;
            modePill.style.marginRight = GraphEditorMetrics.Spacing;
            modePill.style.fontSize = GraphEditorMetrics.SmallFontSize;
            modePill.style.unityFontStyleAndWeight = FontStyle.Bold;
            modePill.style.borderTopLeftRadius = 9f;
            modePill.style.borderTopRightRadius = 9f;
            modePill.style.borderBottomLeftRadius = 9f;
            modePill.style.borderBottomRightRadius = 9f;
            modePill.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(modePill);

            statusLabel = GraphEditorStyles.CreateMutedLabel();
            statusLabel.style.flexGrow = 1f;
            statusLabel.style.marginLeft = GraphEditorMetrics.Spacing;
            statusLabel.style.marginRight = GraphEditorMetrics.Spacing;
            Add(statusLabel);

            diagnosticsButton = GraphEditorStyles.CreateToolButton(
                "Problems",
                "Open the Diagnostics panel.");
            diagnosticsButton.style.height = GraphEditorMetrics.SmallControlHeight;
            diagnosticsButton.style.fontSize = GraphEditorMetrics.FontSize;
            diagnosticsButton.style.paddingLeft = 6f;
            diagnosticsButton.style.paddingRight = 6f;
            diagnosticsButton.clicked += ToggleDiagnostics;
            Add(diagnosticsButton);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            GraphEditorMode mode = Session.Mode;
            SetModePill(mode);

            string status = Session.StatusMessage;
            if (string.IsNullOrEmpty(status) && mode == GraphEditorMode.NoDocument)
                status = "Select a graph asset or a GameObject with a StateMachineRunner.";

            statusLabel.text = status;
            statusLabel.tooltip = status;
            statusLabel.style.color = new StyleColor(
                string.IsNullOrEmpty(status)
                    ? GraphEditorStyles.MutedTextColor
                    : Session.StatusIsError
                        ? GraphEditorStyles.ErrorColor
                        : GraphEditorStyles.MutedTextColor);

            UpdateDiagnosticsButton(mode);
        }

        private void SetModePill(GraphEditorMode mode)
        {
            Color color;
            string text;

            switch (mode)
            {
                case GraphEditorMode.Author:
                    color = GraphEditorStyles.MutedTextColor;
                    text = "Edit";
                    break;
                case GraphEditorMode.Preview:
                    color = GraphEditorStyles.PreviewColor;
                    text = "Preview";
                    break;
                case GraphEditorMode.Live:
                case GraphEditorMode.LiveNoTarget:
                    color = GraphEditorStyles.LiveColor;
                    text = "Live";
                    break;
                default:
                    color = GraphEditorStyles.MutedTextColor;
                    text = "No Graph";
                    break;
            }

            modePill.text = text;
            modePill.style.color = new StyleColor(color);
            modePill.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.12f));
            modePill.tooltip = mode.ToString();
        }

        private void UpdateDiagnosticsButton(GraphEditorMode mode)
        {
            int errors = 0;
            int warnings = 0;
            CountIssues(ref errors, ref warnings);

            GraphConsoleTab content = Session.Console.visible
                ? Session.Console.tab
                : GraphDiagnosticsPolicy.ResolveContent(mode, Session.Validation);
            bool runtimeError = !string.IsNullOrEmpty(Session.PreviewLastError);

            string text;
            Color color;

            if (errors > 0)
            {
                text = "Problems " + errors;
                color = GraphEditorStyles.ErrorColor;
            }
            else if (content == GraphConsoleTab.Validation)
            {
                text = warnings > 0 ? "Problems " + warnings + "w" : "Problems";
                color = runtimeError
                    ? GraphEditorStyles.ErrorColor
                    : warnings > 0
                        ? GraphEditorStyles.WarningColor
                        : GraphEditorStyles.MutedTextColor;
            }
            else
            {
                text = "Runtime";
                color = runtimeError
                    ? GraphEditorStyles.ErrorColor
                    : GraphEditorStyles.MutedTextColor;
            }

            diagnosticsButton.text = text;
            diagnosticsButton.style.color = new StyleColor(color);

            string tooltip = errors + " error(s), " + warnings + " warning(s). "
                + "Click to open or close Diagnostics.";
            if (runtimeError)
                tooltip += "\nRuntime error: " + Session.PreviewLastError;

            diagnosticsButton.tooltip = tooltip;
        }

        private void CountIssues(ref int errors, ref int warnings)
        {
            if (Session.Validation == null)
                return;

            for (int i = 0; i < Session.Validation.Issues.Count; i++)
            {
                if (Session.Validation.Issues[i].severity == GraphIssueSeverity.Error)
                    errors++;
                else
                    warnings++;
            }
        }

        private void ToggleDiagnostics()
        {
            if (Commands == null)
                return;

            if (Session.Console.visible)
            {
                Commands.Layout.ToggleConsole();
                return;
            }

            Commands.Layout.OpenConsole(
                GraphDiagnosticsPolicy.ResolveContent(Session.Mode, Session.Validation));
        }
    }
}
