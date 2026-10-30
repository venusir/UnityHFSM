using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class ValidationPanel : VisualElement
    {
        private readonly StateMachineGraphWindow window;
        private readonly Label summary;
        private readonly ScrollView list;

        public ValidationPanel(StateMachineGraphWindow window)
        {
            this.window = window;
            GraphEditorStyles.SetBorder(this, GraphEditorStyles.SeparatorColor, rightWidth: 1f);

            var header = GraphEditorStyles.CreateHeaderBand("Validation", out VisualElement headerRight);
            Add(header);

            summary = new Label();
            summary.style.fontSize = 11f;
            summary.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            summary.style.marginLeft = 4f;
            summary.style.whiteSpace = WhiteSpace.NoWrap;
            summary.style.textOverflow = TextOverflow.Ellipsis;
            summary.style.overflow = Overflow.Hidden;
            summary.style.flexShrink = 1f;
            summary.style.minWidth = 0f;
            summary.style.maxWidth = 220f;
            headerRight.Add(summary);

            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 8f;
            body.style.paddingRight = 8f;
            body.style.paddingTop = 4f;
            Add(body);

            list = new ScrollView();
            list.style.flexGrow = 1f;
            body.Add(list);

            Refresh();
        }

        public void Refresh()
        {
            list.Clear();

            if (window.Controller == null || window.Controller.Model == null)
            {
                summary.text = string.Empty;
                summary.tooltip = string.Empty;
                list.Add(new Label("No graph asset selected."));
                return;
            }

            GraphValidationResult result = GraphValidator.Validate(window.Controller.Model);
            int errors = 0;
            int warnings = 0;

            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].severity == GraphIssueSeverity.Error)
                    errors++;
                else
                    warnings++;
            }

            summary.text = errors + " error(s), " + warnings + " warning(s)";
            summary.tooltip = summary.text;
            summary.style.color = new StyleColor(
                errors > 0
                    ? GraphEditorStyles.ErrorColor
                    : warnings > 0
                        ? GraphEditorStyles.WarningColor
                        : GraphEditorStyles.SuccessColor);

            if (result.Issues.Count == 0)
            {
                var ok = new Label("No issues.");
                ok.style.color = new StyleColor(GraphEditorStyles.SuccessColor);
                list.Add(ok);
                return;
            }

            for (int i = 0; i < result.Issues.Count; i++)
            {
                GraphIssue issue = result.Issues[i];
                var label = new Label(
                    (issue.severity == GraphIssueSeverity.Error ? "ERROR " : "WARN ")
                    + issue.code
                    + ": "
                    + issue.message);

                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.color = new StyleColor(
                    issue.severity == GraphIssueSeverity.Error
                        ? GraphEditorStyles.ErrorColor
                        : GraphEditorStyles.WarningColor);

                list.Add(label);
            }
        }
    }
}
