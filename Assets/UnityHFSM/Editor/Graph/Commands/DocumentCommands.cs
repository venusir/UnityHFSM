using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class DocumentCommands
    {
        private readonly GraphEditorSession session;

        public DocumentCommands(GraphEditorSession session)
        {
            this.session = session;
        }

        public GraphCommandResult OpenAsset(
            StateMachineGraphAsset asset,
            StateMachineRunner runner = null)
        {
            GraphCommandResult result = session.OpenAsset(asset, runner);
            if (!result.Success)
                session.ReportStatus(result.Message, true);
            return result;
        }

        /// <summary>Passive follow; returns the block reason without spamming status.</summary>
        public GraphCommandResult FollowSelection(UnityEngine.Object selected)
        {
            return session.FollowSelection(selected);
        }

        public GraphCommandResult SetLock(bool locked)
        {
            session.SetSelectionLocked(locked);

            if (!locked)
                session.FollowSelection(UnityEditor.Selection.activeObject);

            session.ReportStatus(
                locked ? "Selection following locked." : "Selection following unlocked.",
                false);
            return GraphCommandResult.Ok();
        }

        public GraphCommandResult Reload()
        {
            return OpenAsset(session.Asset, session.State.followedRunner);
        }
    }
}
