using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Resizable diagnostics drawer. Problems are shown while editing and the
    /// runtime log takes over while a runtime is active; the two never compete
    /// as equal tabs.
    /// </summary>
    public sealed class GraphDiagnosticsView : GraphEditorView
    {
        private readonly Label titleLabel;
        private readonly Button switchButton;
        private readonly VisualElement content;
        private readonly ValidationView validationView;
        private readonly RuntimeLogView runtimeLogView;
        private GraphConsoleTab currentContent;

        public GraphDiagnosticsView()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 0f;
            style.flexShrink = 0f;
            style.minHeight = 0f;

            VisualElement header = GraphEditorStyles.CreateHeaderBand(
                out VisualElement leftSlot,
                out VisualElement rightSlot);
            Add(header);

            titleLabel = new Label("Problems");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = GraphEditorMetrics.FontSize;
            titleLabel.style.flexShrink = 0f;
            leftSlot.Add(titleLabel);

            switchButton = CreateHeaderLink("View Runtime", SwitchContent);
            switchButton.tooltip = "Switch between Problems and Runtime.";
            rightSlot.Add(switchButton);

            Button closeButton = CreateHeaderLink("×", () => Commands?.Layout.CloseConsole());
            closeButton.tooltip = "Close Diagnostics";
            closeButton.style.minWidth = 24f;
            rightSlot.Add(closeButton);

            content = new VisualElement();
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            Add(content);

            validationView = new ValidationView();
            validationView.style.flexGrow = 1f;
            validationView.style.minHeight = 0f;
            content.Add(validationView);

            runtimeLogView = new RuntimeLogView();
            runtimeLogView.style.flexGrow = 1f;
            runtimeLogView.style.minHeight = 0f;
            content.Add(runtimeLogView);
        }

        protected override void OnBind()
        {
            validationView.Bind(Session, Commands);
            runtimeLogView.Bind(Session, Commands);
            currentContent = GraphDiagnosticsPolicy.ResolveContent(Session.Mode, Session.Validation);
            Apply();
        }

        protected override void OnUnbind()
        {
            validationView.Unbind();
            runtimeLogView.Unbind();
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (Session == null || Commands == null)
                return;

            if ((change & (GraphEditorChange.Document
                | GraphEditorChange.Mode
                | GraphEditorChange.All)) != 0)
            {
                SetContent(
                    GraphDiagnosticsPolicy.ResolveContent(Session.Mode, Session.Validation),
                    true);
            }
            else if ((change & GraphEditorChange.Validation) != 0
                && Session.Validation != null
                && Session.Validation.HasErrors)
            {
                SetContent(GraphConsoleTab.Validation, true);
            }
            else if ((change & GraphEditorChange.Console) != 0)
            {
                currentContent = Session.Console.tab;
            }

            Apply();
        }

        private void SetContent(GraphConsoleTab content, bool syncSessionWhileVisible)
        {
            currentContent = content;

            if (syncSessionWhileVisible
                && Session.Console.visible
                && Session.Console.tab != content)
            {
                Commands.Layout.SetConsoleTab(content);
            }
        }

        private void SwitchContent()
        {
            if (Commands == null)
                return;

            Commands.Layout.SetConsoleTab(
                currentContent == GraphConsoleTab.Validation
                    ? GraphConsoleTab.RuntimeLog
                    : GraphConsoleTab.Validation);
        }

        private void Apply()
        {
            if (Session == null)
                return;

            bool visible = Session.Console.visible;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            bool showProblems = currentContent == GraphConsoleTab.Validation;
            validationView.style.display = showProblems ? DisplayStyle.Flex : DisplayStyle.None;
            runtimeLogView.style.display = showProblems ? DisplayStyle.None : DisplayStyle.Flex;

            titleLabel.text = showProblems ? "Problems" : "Runtime";
            switchButton.text = showProblems ? "View Runtime" : "View Problems";
        }

        private static Button CreateHeaderLink(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.height = GraphEditorMetrics.SmallControlHeight;
            button.style.marginLeft = GraphEditorMetrics.SmallSpacing;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 6f;
            button.style.paddingRight = 6f;
            button.style.fontSize = GraphEditorMetrics.FontSize;
            button.style.backgroundColor = new StyleColor(Color.clear);
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.color = new StyleColor(GraphEditorStyles.MutedTextColor);
            button.style.flexShrink = 0f;
            return button;
        }
    }
}
