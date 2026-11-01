namespace UnityHFSM.Graph.Editor
{
    public enum GraphCommandBlockReason
    {
        None = 0,
        NoDocument = 1,
        ReadOnlyMode = 2,
        NoRuntime = 3,
        NoSelection = 4,
        InvalidTarget = 5,
        InvalidName = 6,
        InvalidConnection = 7,
        MissingGraphId = 8,
        Unknown = 9,
    }

    public readonly struct GraphCommandResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly GraphCommandBlockReason Reason;
        public readonly string Value;

        private GraphCommandResult(
            bool success,
            string message,
            GraphCommandBlockReason reason,
            string value)
        {
            Success = success;
            Message = message ?? string.Empty;
            Reason = reason;
            Value = value ?? string.Empty;
        }

        public static GraphCommandResult Ok(string value = null) =>
            new GraphCommandResult(true, string.Empty, GraphCommandBlockReason.None, value);

        public static GraphCommandResult Blocked(
            GraphCommandBlockReason reason,
            string message) =>
            new GraphCommandResult(false, message, reason, null);
    }
}
