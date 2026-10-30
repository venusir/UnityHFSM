using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    /// <summary>
    /// Central mutation layer. Every structural edit records Undo and marks the
    /// asset dirty; the window rebuilds the view from IDs after the change.
    /// </summary>
    public sealed class GraphEditorController
    {
        public StateMachineGraphAsset Asset { get; }
        public GraphModel Model => Asset != null ? Asset.Graph : null;

        public event Action Changed;

        public GraphEditorController(StateMachineGraphAsset asset)
        {
            Asset = asset;
        }

        public void InitializeIfEmpty()
        {
            if (Asset == null)
                return;

            if (Model.machines.Count == 0 && string.IsNullOrEmpty(Model.rootMachineId))
            {
                RecordComplete("Initialize Graph");
                Asset.SetGraph(GraphModelFactory.CreateNew());
                NotifyChanged();
            }
        }

        public string AddState(string machineId, Vector2 position)
        {
            if (!TryGetMachine(machineId, out MachineDef machine))
                return null;

            RecordComplete("Add State");
            string name = UniqueStateName(machineId, "New State", null);
            StateDef state = GraphModelFactory.AddState(Model, machineId, name, position);
            if (string.IsNullOrEmpty(machine.startStateId))
                machine.startStateId = state.id;

            NotifyChanged();
            return state.id;
        }

        public string AddSubMachine(string machineId, Vector2 position)
        {
            if (!TryGetMachine(machineId, out MachineDef machine))
                return null;

            RecordComplete("Add Sub-State Machine");
            string name = UniqueStateName(machineId, "New Sub Machine", null);
            StateDef host = GraphModelFactory.AddState(Model, machineId, name, position);
            GraphModelFactory.AddSubMachine(Model, host, name);

            if (string.IsNullOrEmpty(machine.startStateId))
                machine.startStateId = host.id;

            NotifyChanged();
            return host.id;
        }

        public void DeleteState(string stateId)
        {
            StateDef state = FindState(stateId);
            if (state == null)
                return;

            MachineDef machine = FindMachine(state.machineId);
            if (machine == null)
                return;

            RecordComplete("Delete State");

            if (state.kind == StateKind.HybridSubMachine && !string.IsNullOrEmpty(state.subMachineId))
                DeleteMachineTree(state.subMachineId);

            for (int i = Model.transitions.Count - 1; i >= 0; i--)
            {
                TransitionDef transition = Model.transitions[i];
                if (transition == null)
                    continue;

                if (transition.machineId == state.machineId
                    && (transition.fromStateId == state.id || transition.toStateId == state.id))
                {
                    Model.transitions.RemoveAt(i);
                }
            }

            Model.states.Remove(state);

            if (machine.startStateId == state.id)
                machine.startStateId = FirstStateId(machine.id);

            NotifyChanged();
        }

        public void RenameState(string stateId, string name)
        {
            StateDef state = FindState(stateId);
            if (state == null)
                return;

            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0 || StateNameExists(state.machineId, trimmed, state.id))
                return;

            if (state.name == trimmed)
                return;

            RecordComplete("Rename State");
            state.name = trimmed;

            if (state.kind == StateKind.HybridSubMachine)
            {
                MachineDef child = FindMachine(state.subMachineId);
                if (child != null)
                    child.name = trimmed;
            }

            NotifyChanged();
        }

        public void SetStartState(string machineId, string stateId)
        {
            if (!TryGetMachine(machineId, out MachineDef machine))
                return;

            StateDef state = FindState(stateId);
            if (state == null || state.machineId != machineId)
                return;

            RecordComplete("Set Start State");
            machine.startStateId = stateId;
            NotifyChanged();
        }

        public void MoveState(string stateId, Vector2 position)
        {
            StateDef state = FindState(stateId);
            if (state == null)
                return;

            RecordObject("Move State");
            state.position = position;
            MarkDirty();
        }

        public void MovePseudoNode(string machineId, PseudoNodeKind kind, Vector2 position)
        {
            MachineDef machine = FindMachine(machineId);
            if (machine == null)
                return;

            RecordObject("Move Node");
            switch (kind)
            {
                case PseudoNodeKind.Entry:
                    machine.entryPosition = position;
                    break;
                case PseudoNodeKind.AnyState:
                    machine.anyStatePosition = position;
                    break;
                case PseudoNodeKind.Exit:
                    machine.exitPosition = position;
                    break;
            }

            MarkDirty();
        }

        public string AddTransition(
            string machineId,
            TransitionSourceKind source,
            string fromStateId,
            TransitionTargetKind target,
            string toStateId)
        {
            if (!TryGetMachine(machineId, out MachineDef machine))
                return null;

            if (target == TransitionTargetKind.Exit && string.IsNullOrEmpty(machine.parentStateId))
            {
                Debug.LogWarning("UnityHFSM: the root machine cannot have Exit transitions.");
                return null;
            }

            if (source == TransitionSourceKind.State && string.IsNullOrEmpty(fromStateId))
                return null;

            if (target == TransitionTargetKind.State && string.IsNullOrEmpty(toStateId))
                return null;

            RecordComplete("Add Transition");
            var transition = new TransitionDef
            {
                id = GraphModelFactory.NewId(),
                machineId = machineId,
                source = source,
                fromStateId = source == TransitionSourceKind.State ? fromStateId : string.Empty,
                target = target,
                toStateId = target == TransitionTargetKind.State ? toStateId : string.Empty,
                order = NextOrder(machineId, source, fromStateId),
            };

            Model.transitions.Add(transition);
            NotifyChanged();
            return transition.id;
        }

        public void DeleteTransition(string transitionId)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null)
                return;

            RecordComplete("Delete Transition");
            Model.transitions.Remove(transition);
            NotifyChanged();
        }

        public void SetTransitionDelay(string transitionId, float delay)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null)
                return;

            RecordObject("Edit Transition");
            transition.delaySeconds = float.IsNaN(delay) || float.IsInfinity(delay) ? 0f : Mathf.Max(0f, delay);
            MarkDirty();
        }

        public void SetTransitionForceInstantly(string transitionId, bool value)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null)
                return;

            RecordObject("Edit Transition");
            transition.forceInstantly = value;
            MarkDirty();
        }

        public void SetTransitionOrder(string transitionId, int order)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null)
                return;

            RecordObject("Edit Transition");
            transition.order = order;
            MarkDirty();
        }

        public void AddCondition(string transitionId)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null || Model.parameters.Count == 0)
                return;

            ParameterDef parameter = Model.parameters[0];
            RecordComplete("Add Condition");
            transition.conditions.Add(new ConditionDef
            {
                parameterId = parameter.id,
                comparison = DefaultComparison(parameter.type),
            });
            NotifyChanged();
        }

        public void RemoveCondition(string transitionId, int index)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null || index < 0 || index >= transition.conditions.Count)
                return;

            RecordComplete("Remove Condition");
            transition.conditions.RemoveAt(index);
            NotifyChanged();
        }

        public void SetConditionParameter(string transitionId, int index, string parameterId)
        {
            TransitionDef transition = FindTransition(transitionId);
            ParameterDef parameter = FindParameter(parameterId);
            if (transition == null || parameter == null || index < 0 || index >= transition.conditions.Count)
                return;

            RecordObject("Edit Condition");
            ConditionDef condition = transition.conditions[index];
            condition.parameterId = parameterId;
            condition.comparison = DefaultComparison(parameter.type);
            NotifyChanged();
        }

        public void SetConditionComparison(string transitionId, int index, ConditionComparison comparison)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null || index < 0 || index >= transition.conditions.Count)
                return;

            RecordObject("Edit Condition");
            transition.conditions[index].comparison = comparison;
            MarkDirty();
        }

        public void SetConditionFloat(string transitionId, int index, float value)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null || index < 0 || index >= transition.conditions.Count)
                return;

            RecordObject("Edit Condition");
            transition.conditions[index].floatValue = value;
            MarkDirty();
        }

        public void SetConditionInt(string transitionId, int index, int value)
        {
            TransitionDef transition = FindTransition(transitionId);
            if (transition == null || index < 0 || index >= transition.conditions.Count)
                return;

            RecordObject("Edit Condition");
            transition.conditions[index].intValue = value;
            MarkDirty();
        }

        public void AddParameter()
        {
            AddParameter(BlackboardParameterType.Float);
        }

        public string AddParameter(BlackboardParameterType type)
        {
            RecordComplete("Add Parameter");
            var parameter = new ParameterDef
            {
                id = GraphModelFactory.NewId(),
                name = UniqueParameterName("Parameter"),
                type = type,
            };

            Model.parameters.Add(parameter);
            NotifyChanged();
            return parameter.id;
        }

        public void DeleteParameter(string parameterId)
        {
            ParameterDef parameter = FindParameter(parameterId);
            if (parameter == null)
                return;

            RecordComplete("Delete Parameter");
            Model.parameters.Remove(parameter);

            for (int i = 0; i < Model.transitions.Count; i++)
            {
                TransitionDef transition = Model.transitions[i];
                if (transition == null || transition.conditions == null)
                    continue;

                for (int c = transition.conditions.Count - 1; c >= 0; c--)
                {
                    if (transition.conditions[c] != null
                        && transition.conditions[c].parameterId == parameterId)
                    {
                        transition.conditions.RemoveAt(c);
                    }
                }
            }

            NotifyChanged();
        }

        public void RenameParameter(string parameterId, string name)
        {
            ParameterDef parameter = FindParameter(parameterId);
            if (parameter == null)
                return;

            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0 || ParameterNameExists(trimmed, parameterId))
                return;

            if (parameter.name == trimmed)
                return;

            RecordComplete("Rename Parameter");
            parameter.name = trimmed;
            NotifyChanged();
        }

        public void SetParameterType(string parameterId, BlackboardParameterType type)
        {
            ParameterDef parameter = FindParameter(parameterId);
            if (parameter == null || parameter.type == type)
                return;

            RecordComplete("Change Parameter Type");
            parameter.type = type;

            for (int i = 0; i < Model.transitions.Count; i++)
            {
                TransitionDef transition = Model.transitions[i];
                if (transition == null || transition.conditions == null)
                    continue;

                for (int c = 0; c < transition.conditions.Count; c++)
                {
                    ConditionDef condition = transition.conditions[c];
                    if (condition != null && condition.parameterId == parameterId)
                        condition.comparison = DefaultComparison(type);
                }
            }

            NotifyChanged();
        }

        public void SetParameterFloatDefault(string parameterId, float value)
        {
            ParameterDef parameter = FindParameter(parameterId);
            if (parameter == null)
                return;

            RecordObject("Edit Parameter");
            parameter.defaultFloat = value;
            MarkDirty();
        }

        public void SetParameterIntDefault(string parameterId, int value)
        {
            ParameterDef parameter = FindParameter(parameterId);
            if (parameter == null)
                return;

            RecordObject("Edit Parameter");
            parameter.defaultInt = value;
            MarkDirty();
        }

        public void SetParameterBoolDefault(string parameterId, bool value)
        {
            ParameterDef parameter = FindParameter(parameterId);
            if (parameter == null)
                return;

            RecordObject("Edit Parameter");
            parameter.defaultBool = value;
            MarkDirty();
        }

        public void SetStateNeedsExitTime(string stateId, bool value)
        {
            StateDef state = FindState(stateId);
            if (state == null)
                return;

            RecordObject("Edit State");
            state.needsExitTime = value;
            MarkDirty();
        }

        public void SetStateGhost(string stateId, bool value)
        {
            StateDef state = FindState(stateId);
            if (state == null)
                return;

            RecordObject("Edit State");
            state.isGhostState = value;
            MarkDirty();
        }

        public void SetMachineRememberLastState(string machineId, bool value)
        {
            MachineDef machine = FindMachine(machineId);
            if (machine == null || string.IsNullOrEmpty(machine.parentStateId))
                return;

            RecordObject("Edit Machine");
            machine.rememberLastState = value;
            MarkDirty();
        }

        public void AddBehaviour(string stateId, StateBehaviour behaviour)
        {
            StateDef state = FindState(stateId);
            if (state == null || behaviour == null)
                return;

            RecordComplete("Add Behaviour");
            state.behaviours.Add(behaviour);
            NotifyChanged();
        }

        public void SetBehaviour(string stateId, int index, StateBehaviour behaviour)
        {
            StateDef state = FindState(stateId);
            if (state == null || index < 0 || index >= state.behaviours.Count)
                return;

            RecordObject("Edit Behaviour");
            state.behaviours[index] = behaviour;
            MarkDirty();
        }

        public void RemoveBehaviour(string stateId, int index)
        {
            StateDef state = FindState(stateId);
            if (state == null || index < 0 || index >= state.behaviours.Count)
                return;

            RecordComplete("Remove Behaviour");
            state.behaviours.RemoveAt(index);
            NotifyChanged();
        }

        public StateDef FindState(string stateId)
        {
            return Model != null && !string.IsNullOrEmpty(stateId) ? Model.FindState(stateId) : null;
        }

        public MachineDef FindMachine(string machineId)
        {
            return Model != null && !string.IsNullOrEmpty(machineId) ? Model.FindMachine(machineId) : null;
        }

        public TransitionDef FindTransition(string transitionId)
        {
            return Model != null && !string.IsNullOrEmpty(transitionId) ? Model.FindTransition(transitionId) : null;
        }

        public ParameterDef FindParameter(string parameterId)
        {
            return Model != null && !string.IsNullOrEmpty(parameterId) ? Model.FindParameter(parameterId) : null;
        }

        public void NotifyChanged()
        {
            MarkDirty();
            Changed?.Invoke();
        }

        private void MarkDirty()
        {
            if (Asset != null)
                EditorUtility.SetDirty(Asset);
        }

        private void RecordComplete(string label)
        {
            if (Asset != null)
                Undo.RegisterCompleteObjectUndo(Asset, label);
        }

        private void RecordObject(string label)
        {
            if (Asset != null)
                Undo.RecordObject(Asset, label);
        }

        private bool TryGetMachine(string machineId, out MachineDef machine)
        {
            machine = FindMachine(machineId);
            return machine != null;
        }

        private string FirstStateId(string machineId)
        {
            for (int i = 0; i < Model.states.Count; i++)
            {
                StateDef state = Model.states[i];
                if (state != null && state.machineId == machineId)
                    return state.id;
            }

            return string.Empty;
        }

        private void DeleteMachineTree(string machineId)
        {
            MachineDef machine = FindMachine(machineId);
            if (machine == null)
                return;

            for (int i = Model.states.Count - 1; i >= 0; i--)
            {
                StateDef state = Model.states[i];
                if (state == null || state.machineId != machineId)
                    continue;

                if (state.kind == StateKind.HybridSubMachine && !string.IsNullOrEmpty(state.subMachineId))
                    DeleteMachineTree(state.subMachineId);

                Model.states.RemoveAt(i);
            }

            for (int i = Model.transitions.Count - 1; i >= 0; i--)
            {
                TransitionDef transition = Model.transitions[i];
                if (transition != null && transition.machineId == machineId)
                    Model.transitions.RemoveAt(i);
            }

            Model.machines.Remove(machine);
        }

        private string UniqueStateName(string machineId, string baseName, string excludeStateId)
        {
            string name = baseName;
            int suffix = 1;

            while (StateNameExists(machineId, name, excludeStateId))
            {
                name = baseName + " " + suffix;
                suffix++;
            }

            return name;
        }

        private bool StateNameExists(string machineId, string name, string excludeStateId)
        {
            for (int i = 0; i < Model.states.Count; i++)
            {
                StateDef state = Model.states[i];
                if (state != null
                    && state.machineId == machineId
                    && state.id != excludeStateId
                    && state.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private string UniqueParameterName(string baseName)
        {
            string name = baseName;
            int suffix = 1;

            while (ParameterNameExists(name, null))
            {
                name = baseName + " " + suffix;
                suffix++;
            }

            return name;
        }

        private bool ParameterNameExists(string name, string excludeParameterId)
        {
            for (int i = 0; i < Model.parameters.Count; i++)
            {
                ParameterDef parameter = Model.parameters[i];
                if (parameter != null
                    && parameter.id != excludeParameterId
                    && parameter.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private int NextOrder(string machineId, TransitionSourceKind source, string fromStateId)
        {
            int max = -1;

            for (int i = 0; i < Model.transitions.Count; i++)
            {
                TransitionDef transition = Model.transitions[i];
                if (transition == null || transition.machineId != machineId || transition.source != source)
                    continue;

                if (source == TransitionSourceKind.State && transition.fromStateId != fromStateId)
                    continue;

                if (transition.order > max)
                    max = transition.order;
            }

            return max + 1;
        }

        private static ConditionComparison DefaultComparison(BlackboardParameterType type)
        {
            switch (type)
            {
                case BlackboardParameterType.Bool:
                    return ConditionComparison.IsTrue;
                case BlackboardParameterType.Trigger:
                    return ConditionComparison.IsSet;
                default:
                    return ConditionComparison.Equal;
            }
        }
    }
}
