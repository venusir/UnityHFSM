using System;
using System.Collections.Generic;
using System.Text;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Shared navigation helpers for breadcrumbs, search results and validation
    /// issues. All lookups are read-only.
    /// </summary>
    public static class GraphNavigation
    {
        public static List<MachineDef> BuildMachineChain(GraphModel model, string machineId)
        {
            var chain = new List<MachineDef>();
            if (model == null || string.IsNullOrEmpty(machineId))
                return chain;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            MachineDef machine = model.FindMachine(machineId);

            while (machine != null && seen.Add(machine.id))
            {
                chain.Add(machine);

                if (string.IsNullOrEmpty(machine.parentStateId))
                    break;

                StateDef host = model.FindState(machine.parentStateId);
                machine = host != null ? model.FindMachine(host.machineId) : null;
            }

            chain.Reverse();
            return chain;
        }

        public static string BuildMachinePath(GraphModel model, string machineId)
        {
            List<MachineDef> chain = BuildMachineChain(model, machineId);
            var builder = new StringBuilder();

            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                    builder.Append(" / ");

                builder.Append(string.IsNullOrEmpty(chain[i].name) ? "(unnamed)" : chain[i].name);
            }

            return builder.ToString();
        }

        public static string ResolveMachineIdForState(GraphModel model, string stateId)
        {
            StateDef state = model != null ? model.FindState(stateId) : null;
            return state != null ? state.machineId : null;
        }

        public static string ResolveMachineIdForTransition(GraphModel model, string transitionId)
        {
            TransitionDef transition = model != null ? model.FindTransition(transitionId) : null;
            return transition != null ? transition.machineId : null;
        }

        public static string ResolveMachineIdForIssue(GraphModel model, GraphIssue issue)
        {
            if (model == null || issue == null)
                return null;

            string machineId = ResolveMachineIdForState(model, issue.stateId);
            if (!string.IsNullOrEmpty(machineId))
                return machineId;

            machineId = ResolveMachineIdForTransition(model, issue.transitionId);
            if (!string.IsNullOrEmpty(machineId))
                return machineId;

            return !string.IsNullOrEmpty(issue.machineId) && model.FindMachine(issue.machineId) != null
                ? issue.machineId
                : null;
        }
    }
}
