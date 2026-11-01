using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class RuntimeCommands
    {
        private readonly GraphEditorSession session;

        public RuntimeCommands(GraphEditorSession session)
        {
            this.session = session;
        }

        public GraphCommandResult StartPreview() => session.StartPreview();
        public GraphCommandResult StopPreview() => session.StopPreview();
        public GraphCommandResult TogglePause() => session.TogglePause();
        public GraphCommandResult Step() => session.Step();
        public GraphCommandResult ResetPreview() => session.ResetPreview();
        public GraphCommandResult SetSpeed(float speed) => session.SetPreviewSpeed(speed);
        public GraphCommandResult SetPreviewHost(GameObject host) => session.SetPreviewHost(host);
        public GraphCommandResult SelectTarget(int index) => session.SelectLiveTarget(index);

        public GraphCommandResult SetFloat(string name, float value) =>
            SetLiveValue(name, BlackboardParameterType.Float, (runtime) => runtime.SetFloat(name, value));

        public GraphCommandResult SetInt(string name, int value) =>
            SetLiveValue(name, BlackboardParameterType.Int, (runtime) => runtime.SetInt(name, value));

        public GraphCommandResult SetBool(string name, bool value) =>
            SetLiveValue(name, BlackboardParameterType.Bool, (runtime) => runtime.SetBool(name, value));

        public GraphCommandResult SetTrigger(string name) =>
            SetLiveValue(name, BlackboardParameterType.Trigger, (runtime) => runtime.SetTrigger(name));

        public GraphCommandResult ResetTrigger(string name) =>
            SetLiveValue(name, BlackboardParameterType.Trigger, (runtime) => runtime.ResetTrigger(name));

        private GraphCommandResult SetLiveValue(
            string name,
            BlackboardParameterType expectedType,
            System.Action<GraphRuntime> setter)
        {
            if (!GraphEditorModePolicy.AllowsLiveValues(session.Mode))
            {
                return Blocked(
                    GraphCommandBlockReason.ReadOnlyMode,
                    "Live values are only editable while previewing or playing.");
            }

            GraphRuntime runtime = session.CurrentRuntime;
            if (runtime == null)
                return Blocked(GraphCommandBlockReason.NoRuntime, "No runtime target is selected.");

            if (!runtime.Schema.TryGetIndexByName(name, out int index))
                return Blocked(GraphCommandBlockReason.InvalidTarget, "The runtime has no parameter named '" + name + "'.");

            BlackboardParameterInfo parameter = runtime.Schema.GetParameter(index);
            if (parameter == null || parameter.Type != expectedType)
                return Blocked(GraphCommandBlockReason.InvalidTarget, "The runtime parameter type does not match.");

            try
            {
                setter(runtime);
                return GraphCommandResult.Ok();
            }
            catch (System.Exception exception)
            {
                session.ReportStatus(exception.Message, true);
                return Blocked(GraphCommandBlockReason.Unknown, exception.Message);
            }
        }

        private GraphCommandResult Blocked(GraphCommandBlockReason reason, string message)
        {
            session.ReportStatus(message, true);
            return GraphCommandResult.Blocked(reason, message);
        }
    }
}
