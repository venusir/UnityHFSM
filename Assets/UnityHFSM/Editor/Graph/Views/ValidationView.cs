using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class ValidationView : GraphEditorView
    {
        private readonly ScrollView list;

        public ValidationView()
        {
            style.flexGrow = 1f;
            style.minHeight = 0f;

            list = new ScrollView();
            list.style.flexGrow = 1f;
            list.style.paddingLeft = 8f;
            list.style.paddingRight = 8f;
            list.style.paddingTop = 4f;
            Add(list);
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            if ((change & (GraphEditorChange.Document
                | GraphEditorChange.StructureRebuild
                | GraphEditorChange.Validation
                | GraphEditorChange.All)) != 0)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            list.Clear();

            if (!Session.HasDocument)
            {
                list.Add(new Label("No graph asset selected."));
                return;
            }

            GraphValidationResult result = Session.Validation;
            if (result == null || result.Issues.Count == 0)
            {
                var ok = new Label("No issues.");
                ok.style.color = new StyleColor(GraphEditorStyles.SuccessColor);
                list.Add(ok);
                return;
            }

            for (int i = 0; i < result.Issues.Count; i++)
            {
                GraphIssue issue = result.Issues[i];
                if (issue == null)
                    continue;

                string text =
                    (issue.severity == GraphIssueSeverity.Error ? "ERROR " : "WARN ")
                    + issue.code
                    + ": "
                    + issue.message;

                Color color = issue.severity == GraphIssueSeverity.Error
                    ? GraphEditorStyles.ErrorColor
                    : GraphEditorStyles.WarningColor;

                var button = new Button(() => Commands.Diagnostics.OpenIssue(issue))
                {
                    text = text,
                };
                button.tooltip = "Click to locate the issue.";
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
                button.style.color = new StyleColor(color);
                button.style.fontSize = GraphEditorMetrics.FontSize;
                list.Add(button);
            }
        }
    }
}
