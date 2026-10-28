using System;
using System.Collections.Generic;
using System.Text;

namespace UnityHFSM.Graph
{
    public enum GraphIssueSeverity
    {
        Warning = 0,
        Error = 1,
    }

    [Serializable]
    public sealed class GraphIssue
    {
        public GraphIssueSeverity severity;
        public string code;
        public string message;
        public string machineId;
        public string stateId;
        public string transitionId;

        public GraphIssue(
            GraphIssueSeverity severity,
            string code,
            string message,
            string machineId = null,
            string stateId = null,
            string transitionId = null)
        {
            this.severity = severity;
            this.code = code;
            this.message = message;
            this.machineId = machineId;
            this.stateId = stateId;
            this.transitionId = transitionId;
        }
    }

    public sealed class GraphValidationResult
    {
        private readonly List<GraphIssue> issues = new List<GraphIssue>();

        public IReadOnlyList<GraphIssue> Issues => issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    if (issues[i].severity == GraphIssueSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    if (issues[i].severity == GraphIssueSeverity.Warning)
                        return true;
                }

                return false;
            }
        }

        public void Add(GraphIssue issue)
        {
            if (issue != null)
                issues.Add(issue);
        }

        public void AddError(
            string code,
            string message,
            string machineId = null,
            string stateId = null,
            string transitionId = null)
        {
            issues.Add(new GraphIssue(GraphIssueSeverity.Error, code, message, machineId, stateId, transitionId));
        }

        public void AddWarning(
            string code,
            string message,
            string machineId = null,
            string stateId = null,
            string transitionId = null)
        {
            issues.Add(new GraphIssue(GraphIssueSeverity.Warning, code, message, machineId, stateId, transitionId));
        }
    }

    public sealed class GraphBuildException : Exception
    {
        public GraphValidationResult Validation { get; }

        public GraphBuildException(GraphValidationResult validation)
            : base(BuildMessage(validation))
        {
            Validation = validation;
        }

        private static string BuildMessage(GraphValidationResult validation)
        {
            var builder = new StringBuilder("Graph build failed.");

            if (validation == null)
                return builder.ToString();

            int shown = 0;
            for (int i = 0; i < validation.Issues.Count && shown < 8; i++)
            {
                GraphIssue issue = validation.Issues[i];
                if (issue.severity != GraphIssueSeverity.Error)
                    continue;

                builder.Append("\n- ");
                builder.Append(issue.code);
                builder.Append(": ");
                builder.Append(issue.message);
                shown++;
            }

            if (shown == 0)
                builder.Append(" No error details were provided.");

            return builder.ToString();
        }
    }
}
