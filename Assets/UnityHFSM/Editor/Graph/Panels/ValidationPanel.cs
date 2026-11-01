using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    public sealed class ValidationPanel : VisualElement
    {
        private readonly StateMachineGraphWindow window;
        private readonly ScrollView list;

        public ValidationPanel(StateMachineGraphWindow window)
        {
            this.window = window;

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

            SetValidation(null);
        }

        public event Action<GraphIssue> IssueSelected;

        public void SetValidation(GraphValidationResult result)
        {
            list.Clear();

            if (window.Controller == null || window.Controller.Model == null)
            {
                list.Add(new Label("No graph asset selected."));
                return;
            }

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

                bool navigable =
                    GraphNavigation.ResolveMachineIdForIssue(window.Controller.Model, issue) != null;

                string text =
                    (issue.severity == GraphIssueSeverity.Error ? "ERROR " : "WARN ")
                    + issue.code
                    + ": "
                    + issue.message;

                Color color = issue.severity == GraphIssueSeverity.Error
                    ? GraphEditorStyles.ErrorColor
                    : GraphEditorStyles.WarningColor;

                if (navigable)
                {
                    var button = new Button(() => IssueSelected?.Invoke(issue))
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
                    button.style.fontSize = 11f;
                    list.Add(button);
                }
                else
                {
                    var label = new Label(text);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.color = new StyleColor(color);
                    label.style.fontSize = 11f;
                    list.Add(label);
                }
            }
        }
    }
}
