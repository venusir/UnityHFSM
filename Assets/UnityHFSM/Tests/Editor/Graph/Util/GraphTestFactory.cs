using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph.Tests
{
    public static class GraphTestFactory
    {
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static GraphModel CreateRoot(out MachineDef root)
        {
            var model = new GraphModel
            {
                graphId = NewId(),
            };

            root = new MachineDef
            {
                id = NewId(),
                name = "Root",
                kind = MachineKind.StateMachine,
            };

            model.rootMachineId = root.id;
            model.machines.Add(root);
            return model;
        }

        public static ParameterDef AddParameter(
            GraphModel model,
            string name,
            BlackboardParameterType type,
            float defaultFloat = 0f,
            int defaultInt = 0,
            bool defaultBool = false)
        {
            var parameter = new ParameterDef
            {
                id = NewId(),
                name = name,
                type = type,
                defaultFloat = defaultFloat,
                defaultInt = defaultInt,
                defaultBool = defaultBool,
            };

            model.parameters.Add(parameter);
            return parameter;
        }

        public static StateDef AddState(
            GraphModel model,
            string machineId,
            string name,
            bool needsExitTime = false,
            bool isGhostState = false)
        {
            var state = new StateDef
            {
                id = NewId(),
                machineId = machineId,
                name = name,
                kind = StateKind.State,
                needsExitTime = needsExitTime,
                isGhostState = isGhostState,
            };

            model.states.Add(state);
            return state;
        }

        public static TransitionDef AddTransition(
            GraphModel model,
            string machineId,
            TransitionSourceKind source,
            string fromStateId,
            TransitionTargetKind target,
            string toStateId,
            float delaySeconds = 0f,
            bool forceInstantly = false,
            int order = 0,
            params ConditionDef[] conditions)
        {
            var transition = new TransitionDef
            {
                id = NewId(),
                machineId = machineId,
                source = source,
                fromStateId = fromStateId ?? string.Empty,
                target = target,
                toStateId = toStateId ?? string.Empty,
                delaySeconds = delaySeconds,
                forceInstantly = forceInstantly,
                order = order,
            };

            if (conditions != null)
                transition.conditions.AddRange(conditions);

            model.transitions.Add(transition);
            return transition;
        }

        public static ConditionDef Condition(
            ParameterDef parameter,
            ConditionComparison comparison,
            float floatValue = 0f,
            int intValue = 0)
        {
            return new ConditionDef
            {
                parameterId = parameter.id,
                comparison = comparison,
                floatValue = floatValue,
                intValue = intValue,
            };
        }

        public static GraphModel CreateTwoState(
            out StateDef idle,
            out StateDef move,
            out ParameterDef go,
            out ParameterDef speed)
        {
            GraphModel model = CreateRoot(out MachineDef root);
            idle = AddState(model, root.id, "Idle");
            move = AddState(model, root.id, "Move");
            root.startStateId = idle.id;

            go = AddParameter(model, "Go", BlackboardParameterType.Bool);
            speed = AddParameter(model, "Speed", BlackboardParameterType.Float);

            AddTransition(model, root.id, TransitionSourceKind.State, idle.id, TransitionTargetKind.State, move.id,
                0f, false, 0, Condition(go, ConditionComparison.IsTrue));
            AddTransition(model, root.id, TransitionSourceKind.State, move.id, TransitionTargetKind.State, idle.id,
                0f, false, 0, Condition(speed, ConditionComparison.Greater, 1f));

            return model;
        }

        public static GraphModel CreateNested(
            out MachineDef root,
            out StateDef host,
            out MachineDef child,
            out StateDef childA,
            out StateDef done,
            out ParameterDef leave)
        {
            GraphModel model = CreateRoot(out root);
            host = AddState(model, root.id, "Sub", needsExitTime: true);
            done = AddState(model, root.id, "Done");
            root.startStateId = host.id;

            child = GraphModelFactory.AddSubMachine(model, host, "SubMachine");
            childA = model.FindState(child.startStateId);
            childA.name = "A";
            AddState(model, child.id, "B");

            leave = AddParameter(model, "Leave", BlackboardParameterType.Bool);

            AddTransition(model, root.id, TransitionSourceKind.State, host.id, TransitionTargetKind.State, done.id,
                0f, false, 0, Condition(leave, ConditionComparison.IsTrue));
            AddTransition(model, child.id, TransitionSourceKind.State, childA.id, TransitionTargetKind.Exit, string.Empty,
                0f, false, 0, Condition(leave, ConditionComparison.IsTrue));

            return model;
        }

        public static GraphModel CreateAnyDelay(
            out StateDef a,
            out StateDef b,
            out StateDef c,
            out ParameterDef goB)
        {
            GraphModel model = CreateRoot(out MachineDef root);
            a = AddState(model, root.id, "A");
            b = AddState(model, root.id, "B");
            c = AddState(model, root.id, "C");
            root.startStateId = a.id;

            goB = AddParameter(model, "GoB", BlackboardParameterType.Bool);

            AddTransition(model, root.id, TransitionSourceKind.State, a.id, TransitionTargetKind.State, b.id,
                0f, false, 0, Condition(goB, ConditionComparison.IsTrue));
            AddTransition(model, root.id, TransitionSourceKind.Any, string.Empty, TransitionTargetKind.State, c.id,
                1f, false, 0);

            return model;
        }

        public static GraphModel CreateBehaviourState(
            StateBehaviour behaviour,
            out StateDef a,
            out StateDef b,
            out ParameterDef go)
        {
            GraphModel model = CreateRoot(out MachineDef root);
            a = AddState(model, root.id, "A", needsExitTime: true);
            b = AddState(model, root.id, "B");
            root.startStateId = a.id;
            a.behaviours.Add(behaviour);

            go = AddParameter(model, "Go", BlackboardParameterType.Bool);
            AddTransition(model, root.id, TransitionSourceKind.State, a.id, TransitionTargetKind.State, b.id,
                0f, false, 0, Condition(go, ConditionComparison.IsTrue));

            return model;
        }

        public static void ResetBehaviourLog()
        {
            RecordingBehaviour.Events.Clear();
            RecordingBehaviour.AllowExit = true;
        }
    }
}
