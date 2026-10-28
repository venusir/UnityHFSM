using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Validates a graph model before it is built or imported. The validator is
    /// intentionally independent of the editor so runtime builds and tests use the
    /// same rules.
    /// </summary>
    public static class GraphValidator
    {
        public static GraphValidationResult Validate(GraphModel model)
        {
            var result = new GraphValidationResult();

            if (model == null)
            {
                result.AddError("model-null", "The graph model is null.");
                return result;
            }

            model.EnsureLists();

            if (model.schemaVersion != 1)
                result.AddError("schema-version", "Unsupported graph schema version: " + model.schemaVersion);

            if (string.IsNullOrWhiteSpace(model.graphId))
                result.AddWarning("graph-id", "The graph has no stable graph id.");

            var machineById = new Dictionary<string, MachineDef>(StringComparer.Ordinal);
            var stateById = new Dictionary<string, StateDef>(StringComparer.Ordinal);
            var transitionById = new Dictionary<string, TransitionDef>(StringComparer.Ordinal);
            var parameterById = new Dictionary<string, ParameterDef>(StringComparer.Ordinal);
            var idOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var duplicateIdReported = new HashSet<string>(StringComparer.Ordinal);

            IndexUniqueIds(
                model.machines,
                "machine",
                machineById,
                idOwners,
                duplicateIdReported,
                result,
                m => m.id,
                m => m.name,
                m => m.id);
            IndexUniqueIds(
                model.states,
                "state",
                stateById,
                idOwners,
                duplicateIdReported,
                result,
                s => s.id,
                s => s.name,
                s => s.machineId);
            IndexUniqueIds(
                model.transitions,
                "transition",
                transitionById,
                idOwners,
                duplicateIdReported,
                result,
                t => t.id,
                t => t.id,
                t => t.machineId);
            IndexUniqueIds(
                model.parameters,
                "parameter",
                parameterById,
                idOwners,
                duplicateIdReported,
                result,
                p => p.id,
                p => p.name,
                p => null);

            ValidateParameters(model, parameterById, result);
            ValidateMachinesAndStates(model, machineById, stateById, result);
            ValidateReachabilityAndCycles(model, machineById, stateById, result);
            ValidateTransitions(model, machineById, stateById, parameterById, result);
            return result;
        }

        private static void IndexUniqueIds<T>(
            List<T> items,
            string kind,
            Dictionary<string, T> byId,
            Dictionary<string, string> idOwners,
            HashSet<string> duplicateIdReported,
            GraphValidationResult result,
            Func<T, string> getId,
            Func<T, string> getName,
            Func<T, string> getContext)
            where T : class
        {
            if (items == null)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null)
                {
                    result.AddError("null-" + kind, "A " + kind + " entry is null.");
                    continue;
                }

                string id = getId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    result.AddError(
                        "empty-" + kind + "-id",
                        "A " + kind + " has an empty id.",
                        machineId: kind == "machine" ? getContext(item) : null);
                    continue;
                }

                if (idOwners.TryGetValue(id, out string owner))
                {
                    if (duplicateIdReported.Add(id))
                    {
                        result.AddError(
                            "duplicate-id",
                            "Id '" + id + "' is used by both " + owner + " and " + kind + ".",
                            machineId: getContext(item));
                    }

                    continue;
                }

                idOwners.Add(id, kind);
                byId.Add(id, item);
            }
        }

        private static void ValidateParameters(
            GraphModel model,
            Dictionary<string, ParameterDef> parameterById,
            GraphValidationResult result)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < model.parameters.Count; i++)
            {
                ParameterDef parameter = model.parameters[i];
                if (parameter == null)
                    continue;

                if (string.IsNullOrWhiteSpace(parameter.name))
                {
                    result.AddError("empty-parameter-name", "Parameter '" + parameter.id + "' has an empty name.");
                    continue;
                }

                if (parameter.name != parameter.name.Trim())
                    result.AddError("parameter-name-whitespace", "Parameter name '" + parameter.name + "' has leading or trailing whitespace.");

                if (!names.Add(parameter.name))
                    result.AddError("duplicate-parameter-name", "Parameter name '" + parameter.name + "' is not unique.");
            }
        }

        private static void ValidateMachinesAndStates(
            GraphModel model,
            Dictionary<string, MachineDef> machineById,
            Dictionary<string, StateDef> stateById,
            GraphValidationResult result)
        {
            var statesByMachine = new Dictionary<string, List<StateDef>>(StringComparer.Ordinal);
            var stateNamesByMachine = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef state = model.states[i];
                if (state == null)
                    continue;

                if (!machineById.TryGetValue(state.machineId, out MachineDef machine))
                {
                    result.AddError(
                        "state-unknown-machine",
                        "State '" + state.name + "' references unknown machine '" + state.machineId + "'.",
                        stateId: state.id);
                    continue;
                }

                if (!statesByMachine.TryGetValue(state.machineId, out List<StateDef> machineStates))
                {
                    machineStates = new List<StateDef>();
                    statesByMachine.Add(state.machineId, machineStates);
                }

                machineStates.Add(state);

                if (string.IsNullOrWhiteSpace(state.name))
                {
                    result.AddError("state-empty-name", "State '" + state.id + "' has an empty name.", stateId: state.id);
                }
                else
                {
                    if (state.name != state.name.Trim())
                        result.AddError("state-name-whitespace", "State name '" + state.name + "' has leading or trailing whitespace.", stateId: state.id);

                    if (state.name.IndexOf('/') >= 0)
                        result.AddError("state-name-slash", "State name '" + state.name + "' must not contain '/'.", stateId: state.id);

                    if (!stateNamesByMachine.TryGetValue(state.machineId, out HashSet<string> names))
                    {
                        names = new HashSet<string>(StringComparer.Ordinal);
                        stateNamesByMachine.Add(state.machineId, names);
                    }

                    if (!names.Add(state.name))
                        result.AddError("duplicate-state-name", "State name '" + state.name + "' is not unique in machine '" + machine.name + "'.", stateId: state.id);
                }

                if (state.kind == StateKind.State && !string.IsNullOrEmpty(state.subMachineId))
                    result.AddError("state-unexpected-submachine", "Normal state '" + state.name + "' has a subMachineId.", stateId: state.id);

                if (state.kind == StateKind.HybridSubMachine)
                {
                    if (string.IsNullOrEmpty(state.subMachineId))
                    {
                        result.AddError("submachine-missing", "Sub-state-machine state '" + state.name + "' has no child machine.", stateId: state.id);
                    }
                    else if (!machineById.TryGetValue(state.subMachineId, out MachineDef childMachine))
                    {
                        result.AddError("submachine-unknown", "State '" + state.name + "' references unknown child machine '" + state.subMachineId + "'.", stateId: state.id);
                    }
                    else
                    {
                        if (childMachine.parentStateId != state.id)
                            result.AddError("submachine-parent-mismatch", "Child machine '" + childMachine.name + "' does not point back to host state '" + state.name + "'.", stateId: state.id);
                        if (childMachine.kind != MachineKind.HybridStateMachine)
                            result.AddError("submachine-kind", "Child machine '" + childMachine.name + "' must be HybridStateMachine.", stateId: state.id);
                    }
                }

                if (state.behaviours == null)
                    state.behaviours = new List<StateBehaviour>();

                var seenBehaviours = new HashSet<StateBehaviour>();
                for (int b = 0; b < state.behaviours.Count; b++)
                {
                    StateBehaviour behaviour = state.behaviours[b];
                    if (behaviour == null)
                    {
                        result.AddError("behaviour-missing", "State '" + state.name + "' has a missing behaviour reference.", stateId: state.id);
                        continue;
                    }

                    if (!seenBehaviours.Add(behaviour))
                        result.AddWarning("behaviour-duplicate", "State '" + state.name + "' references behaviour '" + behaviour.name + "' more than once.", stateId: state.id);
                }

                if (!IsFinite(state.position.x) || !IsFinite(state.position.y))
                    result.AddWarning("state-position-invalid", "State '" + state.name + "' has a non-finite position.", stateId: state.id);
            }

            int rootCandidates = 0;
            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef machine = model.machines[i];
                if (machine == null)
                    continue;

                bool isRootCandidate = string.IsNullOrEmpty(machine.parentStateId);
                if (isRootCandidate)
                {
                    rootCandidates++;
                    if (machine.kind != MachineKind.StateMachine)
                        result.AddError("root-kind", "Root machine '" + machine.name + "' must be StateMachine.", machineId: machine.id);
                    if (machine.rememberLastState)
                        result.AddWarning("root-remember-last", "The root machine cannot remember its last state.", machineId: machine.id);
                }
                else
                {
                    if (machine.kind != MachineKind.HybridStateMachine)
                        result.AddError("nested-kind", "Nested machine '" + machine.name + "' must be HybridStateMachine.", machineId: machine.id);

                    if (!stateById.TryGetValue(machine.parentStateId, out StateDef host))
                    {
                        result.AddError("machine-unknown-host", "Machine '" + machine.name + "' references unknown host state '" + machine.parentStateId + "'.", machineId: machine.id);
                    }
                    else
                    {
                        if (host.kind != StateKind.HybridSubMachine)
                            result.AddError("machine-host-kind", "Host state '" + host.name + "' is not a sub-state-machine state.", machineId: machine.id, stateId: host.id);
                        if (host.subMachineId != machine.id)
                            result.AddError("machine-host-backlink", "Host state '" + host.name + "' does not point back to machine '" + machine.name + "'.", machineId: machine.id, stateId: host.id);
                        if (host.machineId == machine.id)
                            result.AddError("machine-self-parent", "Machine '" + machine.name + "' is its own parent.", machineId: machine.id);
                    }
                }

                if (string.IsNullOrEmpty(machine.startStateId))
                {
                    result.AddError("machine-missing-start", "Machine '" + machine.name + "' has no start state.", machineId: machine.id);
                }
                else if (!stateById.TryGetValue(machine.startStateId, out StateDef startState))
                {
                    result.AddError("machine-unknown-start", "Machine '" + machine.name + "' references unknown start state '" + machine.startStateId + "'.", machineId: machine.id);
                }
                else if (startState.machineId != machine.id)
                {
                    result.AddError("machine-start-foreign", "Start state '" + startState.name + "' belongs to a different machine.", machineId: machine.id, stateId: startState.id);
                }

                if (!statesByMachine.TryGetValue(machine.id, out List<StateDef> machineStates) || machineStates.Count == 0)
                    result.AddError("machine-empty", "Machine '" + machine.name + "' has no states.", machineId: machine.id);
            }

            if (rootCandidates != 1)
                result.AddError("root-count", "The graph must contain exactly one root machine, found " + rootCandidates + ".");

            if (!machineById.TryGetValue(model.rootMachineId, out MachineDef rootMachine))
            {
                result.AddError("root-unknown", "rootMachineId '" + model.rootMachineId + "' does not resolve.");
            }
            else if (!string.IsNullOrEmpty(rootMachine.parentStateId))
            {
                result.AddError("root-has-parent", "The machine referenced by rootMachineId is not a root.", machineId: rootMachine.id);
            }
        }

        private static void ValidateReachabilityAndCycles(
            GraphModel model,
            Dictionary<string, MachineDef> machineById,
            Dictionary<string, StateDef> stateById,
            GraphValidationResult result)
        {
            if (!machineById.TryGetValue(model.rootMachineId, out MachineDef root))
                return;

            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<MachineDef>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                MachineDef machine = stack.Pop();
                if (machine == null || !reachable.Add(machine.id))
                    continue;

                for (int i = 0; i < model.states.Count; i++)
                {
                    StateDef state = model.states[i];
                    if (state == null || state.machineId != machine.id || state.kind != StateKind.HybridSubMachine)
                        continue;

                    if (machineById.TryGetValue(state.subMachineId, out MachineDef child))
                        stack.Push(child);
                }
            }

            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef machine = model.machines[i];
                if (machine != null && !reachable.Contains(machine.id))
                    result.AddError("machine-unreachable", "Machine '" + machine.name + "' is not reachable from the root.", machineId: machine.id);
            }

            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef machine = model.machines[i];
                if (machine == null)
                    continue;

                var seen = new HashSet<string>(StringComparer.Ordinal);
                MachineDef current = machine;
                while (current != null && !string.IsNullOrEmpty(current.parentStateId))
                {
                    if (!seen.Add(current.id))
                    {
                        result.AddError("machine-cycle", "Machine containment cycle detected at '" + current.name + "'.", machineId: current.id);
                        break;
                    }

                    if (!stateById.TryGetValue(current.parentStateId, out StateDef host))
                        break;

                    if (!machineById.TryGetValue(host.machineId, out current))
                        break;
                }
            }
        }

        private static void ValidateTransitions(
            GraphModel model,
            Dictionary<string, MachineDef> machineById,
            Dictionary<string, StateDef> stateById,
            Dictionary<string, ParameterDef> parameterById,
            GraphValidationResult result)
        {
            var ordersByBucket = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

            for (int i = 0; i < model.transitions.Count; i++)
            {
                TransitionDef transition = model.transitions[i];
                if (transition == null)
                    continue;

                if (!machineById.TryGetValue(transition.machineId, out MachineDef machine))
                {
                    result.AddError("transition-unknown-machine", "Transition '" + transition.id + "' references unknown machine.", transitionId: transition.id);
                    continue;
                }

                if (float.IsNaN(transition.delaySeconds) || float.IsInfinity(transition.delaySeconds) || transition.delaySeconds < 0f)
                    result.AddError("transition-delay", "Transition '" + transition.id + "' has an invalid delay.", transitionId: transition.id);

                if (transition.source == TransitionSourceKind.Any)
                {
                    if (!string.IsNullOrEmpty(transition.fromStateId))
                        result.AddError("transition-any-from", "Any-State transition '" + transition.id + "' must not have a from state.", transitionId: transition.id);
                }
                else
                {
                    if (string.IsNullOrEmpty(transition.fromStateId))
                    {
                        result.AddError("transition-missing-from", "Transition '" + transition.id + "' has no source state.", transitionId: transition.id);
                    }
                    else if (!stateById.TryGetValue(transition.fromStateId, out StateDef fromState))
                    {
                        result.AddError("transition-unknown-from", "Transition '" + transition.id + "' references unknown source state.", transitionId: transition.id);
                    }
                    else if (fromState.machineId != transition.machineId)
                    {
                        result.AddError("transition-cross-machine", "Transition '" + transition.id + "' source state belongs to another machine.", transitionId: transition.id);
                    }
                }

                if (transition.target == TransitionTargetKind.Exit)
                {
                    if (!string.IsNullOrEmpty(transition.toStateId))
                        result.AddError("transition-exit-to", "Exit transition '" + transition.id + "' must not have a target state.", transitionId: transition.id);

                    if (machine.parentStateId == string.Empty)
                        result.AddError("transition-root-exit", "The root machine cannot contain Exit transitions.", machineId: machine.id, transitionId: transition.id);
                }
                else
                {
                    if (string.IsNullOrEmpty(transition.toStateId))
                    {
                        result.AddError("transition-missing-to", "Transition '" + transition.id + "' has no target state.", transitionId: transition.id);
                    }
                    else if (!stateById.TryGetValue(transition.toStateId, out StateDef toState))
                    {
                        result.AddError("transition-unknown-to", "Transition '" + transition.id + "' references unknown target state.", transitionId: transition.id);
                    }
                    else if (toState.machineId != transition.machineId)
                    {
                        result.AddError("transition-cross-machine", "Transition '" + transition.id + "' target state belongs to another machine.", transitionId: transition.id);
                    }
                }

                string bucket = transition.machineId + "|" + (transition.source == TransitionSourceKind.Any ? "__any__" : transition.fromStateId);
                if (!ordersByBucket.TryGetValue(bucket, out HashSet<int> orders))
                {
                    orders = new HashSet<int>();
                    ordersByBucket.Add(bucket, orders);
                }

                if (!orders.Add(transition.order))
                    result.AddError("transition-order", "Transition order " + transition.order + " is not unique in its evaluation bucket.", transitionId: transition.id);

                if (transition.conditions == null)
                    transition.conditions = new List<ConditionDef>();

                if (transition.conditions.Count == 0 && transition.delaySeconds <= 0f)
                    result.AddWarning("transition-unconditional", "Transition '" + transition.id + "' is unconditional and will fire immediately.", transitionId: transition.id);

                var triggerParameters = new HashSet<string>(StringComparer.Ordinal);

                for (int c = 0; c < transition.conditions.Count; c++)
                {
                    ConditionDef condition = transition.conditions[c];
                    if (condition == null)
                    {
                        result.AddError("condition-null", "Transition '" + transition.id + "' has a null condition.", transitionId: transition.id);
                        continue;
                    }

                    if (!parameterById.TryGetValue(condition.parameterId, out ParameterDef parameter))
                    {
                        result.AddError("condition-unknown-parameter", "Transition '" + transition.id + "' references unknown parameter.", transitionId: transition.id);
                        continue;
                    }

                    if (!IsComparisonCompatible(parameter.type, condition.comparison))
                    {
                        result.AddError(
                            "condition-type",
                            "Condition comparison " + condition.comparison + " is not valid for parameter '" + parameter.name + "' of type " + parameter.type + ".",
                            transitionId: transition.id);
                    }

                    if (parameter.type == BlackboardParameterType.Trigger)
                    {
                        if (!triggerParameters.Add(condition.parameterId))
                            result.AddError("condition-trigger-duplicate", "Transition '" + transition.id + "' references trigger '" + parameter.name + "' more than once.", transitionId: transition.id);
                    }
                }
            }
        }

        private static bool IsComparisonCompatible(BlackboardParameterType type, ConditionComparison comparison)
        {
            switch (type)
            {
                case BlackboardParameterType.Float:
                case BlackboardParameterType.Int:
                    return comparison == ConditionComparison.Equal
                        || comparison == ConditionComparison.NotEqual
                        || comparison == ConditionComparison.Less
                        || comparison == ConditionComparison.LessOrEqual
                        || comparison == ConditionComparison.Greater
                        || comparison == ConditionComparison.GreaterOrEqual;

                case BlackboardParameterType.Bool:
                    return comparison == ConditionComparison.IsTrue
                        || comparison == ConditionComparison.IsFalse;

                case BlackboardParameterType.Trigger:
                    return comparison == ConditionComparison.IsSet;

                default:
                    return false;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
