using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Workspace composition and geometry only: Parameters | Canvas | Inspector,
    /// with a collapsible Diagnostics drawer. No interaction logic beyond splitters.
    /// </summary>
    public sealed class GraphLayoutView : GraphEditorView
    {
        private readonly VisualElement workspace;
        private readonly ParametersView parametersView;
        private readonly InspectorView inspectorView;
        private readonly StateMachineGraphView canvas;
        private readonly GraphDiagnosticsView diagnosticsView;
        private readonly ResizableSplitter parametersSplitter;
        private readonly ResizableSplitter inspectorSplitter;
        private readonly ResizableSplitter consoleSplitter;
        private bool applyingLayout;
        private bool clamping;

        public GraphLayoutView()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1f;
            style.minHeight = 0f;

            workspace = new VisualElement();
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.flexGrow = 1f;
            workspace.style.minHeight = 0f;
            Add(workspace);

            parametersView = new ParametersView();
            workspace.Add(parametersView);

            parametersSplitter = new ResizableSplitter(
                ResizableSplitter.SplitOrientation.Vertical,
                parametersView,
                true,
                250f,
                GraphEditorMetrics.ParametersMinWidth,
                MaxParametersWidth,
                size => Commands?.Layout.SetParametersWidth(size));
            workspace.Add(parametersSplitter);

            canvas = new StateMachineGraphView();
            canvas.style.flexGrow = 1f;
            canvas.style.minWidth = 0f;
            workspace.Add(canvas);

            inspectorSplitter = new ResizableSplitter(
                ResizableSplitter.SplitOrientation.Vertical,
                inspectorView = new InspectorView(),
                false,
                320f,
                GraphEditorMetrics.InspectorMinWidth,
                MaxInspectorWidth,
                size => Commands?.Layout.SetInspectorWidth(size));
            workspace.Add(inspectorSplitter);
            workspace.Add(inspectorView);

            consoleSplitter = new ResizableSplitter(
                ResizableSplitter.SplitOrientation.Horizontal,
                diagnosticsView = new GraphDiagnosticsView(),
                false,
                220f,
                GraphEditorMetrics.ConsoleMinHeight,
                MaxConsoleHeight,
                size => Commands?.Layout.SetConsoleHeight(size));
            Add(consoleSplitter);
            Add(diagnosticsView);

            workspace.RegisterCallback<GeometryChangedEvent>(_ => ClampLayout());
            RegisterCallback<GeometryChangedEvent>(_ => ClampLayout());
        }

        public StateMachineGraphView Canvas => canvas;

        protected override void OnBind()
        {
            Commands.Navigation.Canvas = canvas;
            parametersView.Bind(Session, Commands);
            inspectorView.Bind(Session, Commands);
            canvas.Bind(Session, Commands);
            diagnosticsView.Bind(Session, Commands);
            ApplyLayout();
        }

        protected override void OnUnbind()
        {
            if (Commands?.Navigation != null && ReferenceEquals(Commands.Navigation.Canvas, canvas))
                Commands.Navigation.Canvas = null;

            parametersView.Unbind();
            inspectorView.Unbind();
            canvas.Unbind();
            diagnosticsView.Unbind();
        }

        protected override void OnSessionChanged(GraphEditorChange change)
        {
            if (clamping)
                return;

            if ((change & (GraphEditorChange.Layout
                | GraphEditorChange.Console
                | GraphEditorChange.Document
                | GraphEditorChange.Mode
                | GraphEditorChange.All)) != 0)
            {
                ApplyLayout();

                if (!clamping)
                    ClampLayout();
            }
        }

        private void ApplyLayout()
        {
            if (Session == null || Commands == null || applyingLayout)
                return;

            applyingLayout = true;
            try
            {
                GraphEditorLayoutState layout = Session.Layout;
                bool parametersVisible = layout.parametersVisible;
                bool inspectorVisible = layout.inspectorVisible;
                bool consoleVisible = Session.Console.visible;

                parametersView.style.display = parametersVisible ? DisplayStyle.Flex : DisplayStyle.None;
                parametersSplitter.style.display = parametersVisible ? DisplayStyle.Flex : DisplayStyle.None;
                inspectorView.style.display = inspectorVisible ? DisplayStyle.Flex : DisplayStyle.None;
                inspectorSplitter.style.display = inspectorVisible ? DisplayStyle.Flex : DisplayStyle.None;
                diagnosticsView.style.display = consoleVisible ? DisplayStyle.Flex : DisplayStyle.None;
                consoleSplitter.style.display = consoleVisible ? DisplayStyle.Flex : DisplayStyle.None;

                if (parametersVisible)
                    parametersSplitter.SetSizeWithoutNotify(layout.parametersWidth);

                if (inspectorVisible)
                    inspectorSplitter.SetSizeWithoutNotify(layout.inspectorWidth);

                if (consoleVisible)
                    consoleSplitter.SetSizeWithoutNotify(layout.consoleHeight);
            }
            finally
            {
                applyingLayout = false;
            }
        }

        private void ClampLayout()
        {
            if (applyingLayout || clamping || Session == null || Commands == null)
                return;

            clamping = true;
            try
            {
                GraphEditorLayoutState state = Session.Layout;
                float width = workspace.layout.width;

                if (width > 0f)
                {
                    if (state.parametersVisible)
                    {
                        float reserved = state.inspectorVisible
                            ? Mathf.Max(GraphEditorMetrics.InspectorMinWidth, state.inspectorWidth)
                            : 0f;
                        float max = Mathf.Max(
                            GraphEditorMetrics.ParametersMinWidth,
                            width - GraphEditorMetrics.GraphMinWidth - GraphEditorMetrics.SplitterGutter - reserved);
                        float next = Mathf.Clamp(state.parametersWidth, GraphEditorMetrics.ParametersMinWidth, max);

                        if (!Mathf.Approximately(next, state.parametersWidth))
                            Commands.Layout.SetParametersWidth(next);
                    }

                    if (state.inspectorVisible)
                    {
                        float reserved = state.parametersVisible ? state.parametersWidth : 0f;
                        float max = Mathf.Max(
                            GraphEditorMetrics.InspectorMinWidth,
                            width - GraphEditorMetrics.GraphMinWidth - GraphEditorMetrics.SplitterGutter - reserved);
                        float next = Mathf.Clamp(state.inspectorWidth, GraphEditorMetrics.InspectorMinWidth, max);

                        if (!Mathf.Approximately(next, state.inspectorWidth))
                            Commands.Layout.SetInspectorWidth(next);
                    }
                }

                if (Session.Console.visible && layout.height > 0f)
                {
                    float max = Mathf.Max(
                        GraphEditorMetrics.ConsoleMinHeight,
                        layout.height - 220f);
                    float next = Mathf.Clamp(state.consoleHeight, GraphEditorMetrics.ConsoleMinHeight, max);

                    if (!Mathf.Approximately(next, state.consoleHeight))
                        Commands.Layout.SetConsoleHeight(next);
                }

                ApplyLayout();
            }
            finally
            {
                clamping = false;
            }
        }

        private float MaxParametersWidth()
        {
            if (workspace.layout.width <= 0f)
                return float.PositiveInfinity;

            float reserved = Session != null && Session.Layout.inspectorVisible
                ? Mathf.Max(GraphEditorMetrics.InspectorMinWidth, Session.Layout.inspectorWidth)
                : 0f;
            return Mathf.Max(
                GraphEditorMetrics.ParametersMinWidth,
                workspace.layout.width - GraphEditorMetrics.GraphMinWidth - GraphEditorMetrics.SplitterGutter - reserved);
        }

        private float MaxInspectorWidth()
        {
            if (workspace.layout.width <= 0f)
                return float.PositiveInfinity;

            float reserved = Session != null && Session.Layout.parametersVisible
                ? Session.Layout.parametersWidth
                : 0f;
            return Mathf.Max(
                GraphEditorMetrics.InspectorMinWidth,
                workspace.layout.width - GraphEditorMetrics.GraphMinWidth - GraphEditorMetrics.SplitterGutter - reserved);
        }

        private float MaxConsoleHeight()
        {
            if (layout.height <= 0f)
                return float.PositiveInfinity;

            return Mathf.Max(
                GraphEditorMetrics.ConsoleMinHeight,
                layout.height - 220f);
        }
    }
}
