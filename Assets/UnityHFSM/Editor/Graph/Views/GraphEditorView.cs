using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Base for session-bound views. Bind renders the current state immediately,
    /// so views never miss events published before they were created.
    /// </summary>
    public abstract class GraphEditorView : VisualElement
    {
        private bool bound;

        protected GraphEditorSession Session { get; private set; }
        protected GraphEditorCommands Commands { get; private set; }
        protected bool IsAuthoring => Session != null && GraphEditorModePolicy.AllowsAuthoring(Session.Mode);
        protected bool IsReadOnly => Session != null && GraphEditorModePolicy.IsReadOnlyGraph(Session.Mode);

        public void Bind(GraphEditorSession session, GraphEditorCommands commands)
        {
            if (bound)
                Unbind();

            Session = session;
            Commands = commands;
            bound = session != null;

            if (!bound)
                return;

            OnBind();
            session.Changed += OnSessionChanged;
            OnSessionChanged(GraphEditorChange.All);
        }

        public void Unbind()
        {
            if (!bound)
                return;

            Session.Changed -= OnSessionChanged;
            OnUnbind();
            Session = null;
            Commands = null;
            bound = false;
        }

        protected virtual void OnBind()
        {
        }

        protected virtual void OnUnbind()
        {
        }

        protected abstract void OnSessionChanged(GraphEditorChange change);
    }
}
