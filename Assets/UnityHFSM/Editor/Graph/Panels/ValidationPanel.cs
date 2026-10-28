using UnityEngine;
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
            style.minHeight = 120f;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 4f;

            summary = new Label("Validation");
            summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(summary);

            list = new ScrollView();
            list.style.flexGrow = 1f;
            Add(list);

            Refresh();
        }

        public void Refresh()
        {
            list.Clear();

            if (window.Controller == null || window.Controller.Model == null)
            {
                summary.text = "Validation";
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

            summary.text = "Validation: " + errors + " error(s), " + warnings + " warning(s)";

            if (result.Issues.Count == 0)
            {
                var ok = new Label("No issues.");
                ok.style.color = new StyleColor(new Color(0.45f, 0.85f, 0.45f));
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
                        ? new Color(0.95f, 0.42f, 0.42f)
                        : new Color(0.95f, 0.75f, 0.30f));

                list.Add(label);
            }
        }
    }
}
