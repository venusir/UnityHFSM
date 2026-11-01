using System;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Facade over the user-intent command categories. Views call these methods;
    /// they never mutate GraphEditorController or runtime services directly.
    /// </summary>
    public sealed class GraphEditorCommands : IDisposable
    {
        public GraphEditorCommands(GraphEditorSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Document = new DocumentCommands(session);
            Navigation = new NavigationCommands(session);
            Authoring = new AuthoringCommands(session);
            Runtime = new RuntimeCommands(session);
            Layout = new LayoutCommands(session);
            Diagnostics = new DiagnosticsCommands(session, Navigation);
            Search = new SearchService(session, Navigation);
        }

        public GraphEditorSession Session { get; }
        public DocumentCommands Document { get; }
        public NavigationCommands Navigation { get; }
        public AuthoringCommands Authoring { get; }
        public RuntimeCommands Runtime { get; }
        public LayoutCommands Layout { get; }
        public DiagnosticsCommands Diagnostics { get; }
        public SearchService Search { get; }

        public void Dispose()
        {
            Search.Dispose();
        }
    }
}
