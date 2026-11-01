using System.Globalization;
using System.Text;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Builds a runtime-relevant fingerprint of a graph. Editor-only data
    /// (node positions and pseudo-node positions) is ignored so dragging nodes
    /// does not mark a preview as stale.
    /// </summary>
    public static class GraphModelFingerprint
    {
        public static string Compute(GraphModel model)
        {
            if (model == null)
                return string.Empty;

            model.EnsureLists();

            var builder = new StringBuilder(256);
            builder.Append("graph|").Append(model.graphId).Append('|').Append(model.rootMachineId).Append('\n');

            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef machine = model.machines[i];
                if (machine == null)
                {
                    builder.Append("m|null\n");
                    continue;
                }

                builder.Append("m|")
                    .Append(machine.id).Append('|')
                    .Append(machine.name).Append('|')
                    .Append(machine.parentStateId).Append('|')
                    .Append(machine.startStateId).Append('|')
                    .Append((int)machine.kind).Append('|')
                    .Append(machine.rememberLastState ? '1' : '0')
                    .Append('\n');
            }

            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef state = model.states[i];
                if (state == null)
                {
                    builder.Append("s|null\n");
                    continue;
                }

                builder.Append("s|")
                    .Append(state.id).Append('|')
                    .Append(state.machineId).Append('|')
                    .Append(state.name).Append('|')
                    .Append((int)state.kind).Append('|')
                    .Append(state.subMachineId).Append('|')
                    .Append(state.needsExitTime ? '1' : '0')
                    .Append(state.isGhostState ? '1' : '0')
                    .Append('|');

                for (int b = 0; b < state.behaviours.Count; b++)
                {
                    StateBehaviour behaviour = state.behaviours[b];
#if UNITY_6000_0_OR_NEWER
                    builder.Append(behaviour != null ? behaviour.GetEntityId().ToString() : "0")
                        .Append(',');
#else
                    builder.Append(behaviour != null ? behaviour.GetInstanceID() : 0).Append(',');
#endif
                }

                builder.Append('\n');
            }

            for (int i = 0; i < model.transitions.Count; i++)
            {
                TransitionDef transition = model.transitions[i];
                if (transition == null)
                {
                    builder.Append("t|null\n");
                    continue;
                }

                builder.Append("t|")
                    .Append(transition.id).Append('|')
                    .Append(transition.machineId).Append('|')
                    .Append((int)transition.source).Append('|')
                    .Append(transition.fromStateId).Append('|')
                    .Append((int)transition.target).Append('|')
                    .Append(transition.toStateId).Append('|')
                    .Append(transition.delaySeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(transition.forceInstantly ? '1' : '0').Append('|')
                    .Append(transition.order).Append('|');

                for (int c = 0; c < transition.conditions.Count; c++)
                {
                    ConditionDef condition = transition.conditions[c];
                    if (condition == null)
                    {
                        builder.Append("null,");
                        continue;
                    }

                    builder.Append(condition.parameterId).Append(':')
                        .Append((int)condition.comparison).Append(':')
                        .Append(condition.floatValue.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                        .Append(condition.intValue).Append(',');
                }

                builder.Append('\n');
            }

            for (int i = 0; i < model.parameters.Count; i++)
            {
                ParameterDef parameter = model.parameters[i];
                if (parameter == null)
                {
                    builder.Append("p|null\n");
                    continue;
                }

                builder.Append("p|")
                    .Append(parameter.id).Append('|')
                    .Append(parameter.name).Append('|')
                    .Append((int)parameter.type).Append('|')
                    .Append(parameter.defaultFloat.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(parameter.defaultInt).Append('|')
                    .Append(parameter.defaultBool ? '1' : '0')
                    .Append('\n');
            }

            return builder.ToString();
        }
    }
}
