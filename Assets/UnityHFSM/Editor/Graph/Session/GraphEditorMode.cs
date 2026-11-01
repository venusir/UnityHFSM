using UnityHFSM.Graph;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// The editor's single interaction mode. Every command and view asks the
    /// policy below instead of branching on Application.isPlaying or runtime
    /// object nullability.
    /// </summary>
    public enum GraphEditorMode
    {
        NoDocument = 0,
        Author = 1,
        Preview = 2,
        Live = 3,
        LiveNoTarget = 4,
    }

    public enum GraphEditorParametersMode
    {
        Empty = 0,
        Defaults = 1,
        Live = 2,
        ReadOnlyDefaults = 3,
    }

    public static class GraphEditorModePolicy
    {
        public static GraphEditorMode Resolve(
            bool hasDocument,
            bool isPlayMode,
            bool previewRunning,
            bool hasLiveTarget)
        {
            if (!hasDocument)
                return GraphEditorMode.NoDocument;

            if (!isPlayMode)
                return previewRunning ? GraphEditorMode.Preview : GraphEditorMode.Author;

            return hasLiveTarget ? GraphEditorMode.Live : GraphEditorMode.LiveNoTarget;
        }

        public static bool IsReadOnlyGraph(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview
            || mode == GraphEditorMode.Live
            || mode == GraphEditorMode.LiveNoTarget;

        public static bool AllowsAuthoring(GraphEditorMode mode) =>
            mode == GraphEditorMode.Author;

        public static bool AllowsLiveValues(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview || mode == GraphEditorMode.Live;

        public static bool ShowsRuntimeDetails(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview
            || mode == GraphEditorMode.Live
            || mode == GraphEditorMode.LiveNoTarget;

        public static bool AllowsDocumentFollow(GraphEditorMode mode) =>
            mode == GraphEditorMode.NoDocument || mode == GraphEditorMode.Author;

        public static bool CanStartPreview(GraphEditorMode mode) =>
            mode == GraphEditorMode.Author;

        public static bool CanStopPreview(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview;

        public static bool CanPause(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview || mode == GraphEditorMode.Live;

        public static bool CanStep(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview || mode == GraphEditorMode.Live;

        public static bool CanResetPreview(GraphEditorMode mode) =>
            mode == GraphEditorMode.Preview;

        public static bool CanSelectTarget(GraphEditorMode mode) =>
            mode == GraphEditorMode.Live || mode == GraphEditorMode.LiveNoTarget;

        public static bool CanEditPreviewSettings(GraphEditorMode mode) =>
            mode == GraphEditorMode.Author;

        public static GraphEditorParametersMode GetParametersMode(GraphEditorMode mode)
        {
            switch (mode)
            {
                case GraphEditorMode.Author:
                    return GraphEditorParametersMode.Defaults;
                case GraphEditorMode.Preview:
                case GraphEditorMode.Live:
                    return GraphEditorParametersMode.Live;
                case GraphEditorMode.LiveNoTarget:
                    return GraphEditorParametersMode.ReadOnlyDefaults;
                default:
                    return GraphEditorParametersMode.Empty;
            }
        }
    }

    /// <summary>
    /// Chooses the Diagnostics content that matches the current mode. Validation
    /// errors always win; otherwise Edit shows Problems and a running graph shows
    /// the Runtime log.
    /// </summary>
    public static class GraphDiagnosticsPolicy
    {
        public static GraphConsoleTab ResolveContent(
            GraphEditorMode mode,
            GraphValidationResult validation)
        {
            if (validation != null && validation.HasErrors)
                return GraphConsoleTab.Validation;

            if (mode == GraphEditorMode.Author || mode == GraphEditorMode.NoDocument)
                return GraphConsoleTab.Validation;

            return GraphConsoleTab.RuntimeLog;
        }
    }
}
