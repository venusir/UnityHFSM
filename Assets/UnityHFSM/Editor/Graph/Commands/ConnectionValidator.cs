namespace UnityHFSM.Graph.Editor
{
    public enum GraphConnectSourceKind
    {
        State = 0,
        Entry = 1,
        AnyState = 2,
    }

    public enum GraphConnectTargetKind
    {
        State = 0,
        Exit = 1,
    }

    public readonly struct GraphConnectionRequest
    {
        public readonly GraphConnectSourceKind Source;
        public readonly string SourceStateId;
        public readonly GraphConnectTargetKind Target;
        public readonly string TargetStateId;

        public GraphConnectionRequest(
            GraphConnectSourceKind source,
            string sourceStateId,
            GraphConnectTargetKind target,
            string targetStateId)
        {
            Source = source;
            SourceStateId = sourceStateId ?? string.Empty;
            Target = target;
            TargetStateId = targetStateId ?? string.Empty;
        }
    }

    public static class ConnectionValidator
    {
        public static GraphCommandResult Validate(
            GraphModel model,
            string machineId,
            GraphConnectionRequest request)
        {
            if (model == null || string.IsNullOrEmpty(machineId))
                return Blocked("No graph machine is selected.");

            MachineDef machine = model.FindMachine(machineId);
            if (machine == null)
                return Blocked("The target machine no longer exists.");

            if (request.Source == GraphConnectSourceKind.Entry
                && request.Target == GraphConnectTargetKind.Exit)
            {
                return Blocked("Entry cannot connect directly to Exit.");
            }

            if (request.Source == GraphConnectSourceKind.State)
            {
                StateDef source = model.FindState(request.SourceStateId);
                if (source == null || source.machineId != machineId)
                    return Blocked("The source state is not in this machine.");
            }

            if (request.Target == GraphConnectTargetKind.State)
            {
                StateDef target = model.FindState(request.TargetStateId);
                if (target == null || target.machineId != machineId)
                    return Blocked("The target state is not in this machine.");
            }
            else if (string.IsNullOrEmpty(machine.parentStateId))
            {
                return Blocked("The root machine cannot have Exit transitions.");
            }

            return GraphCommandResult.Ok();
        }

        private static GraphCommandResult Blocked(string message) =>
            GraphCommandResult.Blocked(GraphCommandBlockReason.InvalidConnection, message);
    }
}
