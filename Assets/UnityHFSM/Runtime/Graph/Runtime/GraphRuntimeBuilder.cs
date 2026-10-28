using System;
using System.Collections.Generic;
using System.Linq;
using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Builds a UnityHFSM tree from a GraphModel. The model is read only during
    /// this method; runtime objects copy everything they need.
    /// </summary>
    internal static class GraphRuntimeBuilder
    {
        public static void Build(GraphRuntime runtime, GraphModel model)
        {
            model.EnsureLists();

            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef definition = model.machines[i];
                if (definition == null)
                    continue;

                runtime.RegisterMachine(new MachineRuntime(definition)
                {
                    Runtime = runtime,
                });
            }

            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef definition = model.states[i];
                if (definition == null)
                    continue;

                if (!runtime.TryGetMachine(definition.machineId, out MachineRuntime machine))
                    throw new InvalidOperationException("State references unknown machine: " + definition.id);

                var state = new StateRuntime(definition, machine);
                machine.States.Add(state);
                machine.StateById[state.Id] = state;
                if (!string.IsNullOrEmpty(state.Name))
                {
                    machine.StateByName[state.Name] = state;
                    machine.StateNameToId[state.Name] = state.Id;
                }

                runtime.RegisterState(state);

                if (definition.behaviours != null)
                {
                    for (int b = 0; b < definition.behaviours.Count; b++)
                    {
                        StateBehaviour asset = definition.behaviours[b];
                        if (asset == null)
                            continue;

                        StateBehaviour clone = runtime.BehaviourFactory.Create(asset);
                        if (clone == null)
                            throw new InvalidOperationException("Could not clone behaviour '" + asset.name + "'.");

                        runtime.RegisterBehaviourClone(clone);
                        state.Behaviours.Add(clone);
                    }
                }

                state.Context = new StateBehaviourContext(runtime, machine, state, runtime.Options.Host);
            }

            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef definition = model.machines[i];
                if (definition == null || string.IsNullOrEmpty(definition.parentStateId))
                    continue;

                if (!runtime.TryGetMachine(definition.id, out MachineRuntime machine)
                    || !runtime.TryGetState(definition.parentStateId, out StateRuntime host))
                {
                    throw new InvalidOperationException("Machine parent link could not be resolved: " + definition.id);
                }

                machine.Parent = host.Machine;
                machine.HostState = host;
                host.ChildMachine = machine;
            }

            if (!runtime.TryGetMachine(model.rootMachineId, out MachineRuntime root))
                throw new InvalidOperationException("Root machine could not be resolved: " + model.rootMachineId);

            BuildMachine(runtime, root, model);
            runtime.SetRoot(root);
        }

        private static void BuildMachine(GraphRuntime runtime, MachineRuntime machine, GraphModel model)
        {
            for (int i = 0; i < machine.States.Count; i++)
            {
                StateRuntime state = machine.States[i];
                if (state.Kind == StateKind.HybridSubMachine)
                {
                    if (state.ChildMachine == null)
                        throw new InvalidOperationException("Sub-machine state has no child machine: " + state.Id);
                    BuildMachine(runtime, state.ChildMachine, model);
                }
            }

            StateMachine<string, string, string> fsm;

            if (machine.Parent == null)
            {
                fsm = new StateMachine<string, string, string>(false, false, false);
            }
            else
            {
                StateRuntime host = machine.HostState;

                Action<HybridStateMachine<string, string, string>> beforeEnter = _ =>
                {
                    host.Machine.MarkStateEntered(host.Id);
                    InvokeBehaviours(host, (behaviour, context) => behaviour.OnStateMachineEnter(context), false);
                };

                Action<HybridStateMachine<string, string, string>> beforeLogic = _ =>
                {
                    InvokeBehaviours(host, (behaviour, context) => behaviour.OnStateLogic(context), false);
                };

                Action<HybridStateMachine<string, string, string>> afterExit = _ =>
                {
                    InvokeBehaviours(host, (behaviour, context) => behaviour.OnStateMachineExit(context), true);
                };

                fsm = new HybridStateMachine<string, string, string>(
                    beforeEnter,
                    null,
                    beforeLogic,
                    null,
                    null,
                    afterExit,
                    host.NeedsExitTime,
                    host.IsGhostState,
                    machine.RememberLastState);
            }

            machine.Fsm = fsm;

            for (int i = 0; i < machine.States.Count; i++)
            {
                StateRuntime state = machine.States[i];

                if (state.Kind == StateKind.State)
                {
                    var dataState = new DataState(state);
                    fsm.AddState(state.Name, dataState);
                    state.StateObject = dataState;
                }
                else
                {
                    if (state.ChildMachine == null || state.ChildMachine.Fsm == null)
                        throw new InvalidOperationException("Sub-machine state has no built child FSM: " + state.Id);

                    fsm.AddState(state.Name, state.ChildMachine.Fsm);
                    state.StateObject = state.ChildMachine.Fsm;
                }

                machine.StateObjectsByName[state.Name] = state.StateObject;
                machine.StateIdByObject[state.StateObject] = state.Id;
            }

            if (!machine.StateById.TryGetValue(machine.StartStateId, out StateRuntime startState))
                throw new InvalidOperationException("Machine start state could not be resolved: " + machine.Id);

            fsm.SetStartState(startState.Name);

            machine.StateChangedHandler = changedState =>
            {
                if (changedState != null && machine.StateNameToId.TryGetValue(changedState.name, out string stateId))
                {
                    machine.ActiveStateId = stateId;
                    machine.StateEnteredAt = runtime.Now;
                }
            };
            fsm.StateChanged += machine.StateChangedHandler;

            var anyTransitions = new List<TransitionDef>();
            var directTransitions = new Dictionary<string, List<TransitionDef>>(StringComparer.Ordinal);

            for (int i = 0; i < model.transitions.Count; i++)
            {
                TransitionDef transition = model.transitions[i];
                if (transition == null || transition.machineId != machine.Id)
                    continue;

                if (transition.source == TransitionSourceKind.Any)
                {
                    anyTransitions.Add(transition);
                }
                else
                {
                    if (!directTransitions.TryGetValue(transition.fromStateId, out List<TransitionDef> list))
                    {
                        list = new List<TransitionDef>();
                        directTransitions.Add(transition.fromStateId, list);
                    }

                    list.Add(transition);
                }
            }

            anyTransitions.Sort((a, b) => a.order.CompareTo(b.order));
            for (int i = 0; i < anyTransitions.Count; i++)
                AddTransition(runtime, machine, anyTransitions[i]);

            foreach (List<TransitionDef> list in directTransitions.Values)
            {
                list.Sort((a, b) => a.order.CompareTo(b.order));
                for (int i = 0; i < list.Count; i++)
                    AddTransition(runtime, machine, list[i]);
            }
        }

        private static void AddTransition(GraphRuntime runtime, MachineRuntime machine, TransitionDef definition)
        {
            StateRuntime fromState = definition.source == TransitionSourceKind.State
                ? machine.GetState(definition.fromStateId)
                : null;
            StateRuntime toState = definition.target == TransitionTargetKind.State
                ? machine.GetState(definition.toStateId)
                : null;

            string fromName = fromState != null ? fromState.Name : string.Empty;
            string toName = toState != null ? toState.Name : string.Empty;
            var evaluator = new ConditionEvaluator(runtime.Schema, definition.conditions);
            var transition = new GraphTransition(runtime, machine, definition, fromName, toName, evaluator);

            if (definition.source == TransitionSourceKind.Any)
            {
                if (definition.target == TransitionTargetKind.State)
                    machine.Fsm.AddTransitionFromAny(transition);
                else
                    machine.Fsm.AddExitTransitionFromAny(transition);
            }
            else
            {
                if (definition.target == TransitionTargetKind.State)
                    machine.Fsm.AddTransition(transition);
                else
                    machine.Fsm.AddExitTransition(transition);
            }
        }

        private static void InvokeBehaviours(
            StateRuntime state,
            Action<StateBehaviour, StateBehaviourContext> invoke,
            bool reverse)
        {
            StateBehaviourContext context = state.Context;

            if (reverse)
            {
                for (int i = state.Behaviours.Count - 1; i >= 0; i--)
                {
                    StateBehaviour behaviour = state.Behaviours[i];
                    if (behaviour != null)
                        invoke(behaviour, context);
                }
            }
            else
            {
                for (int i = 0; i < state.Behaviours.Count; i++)
                {
                    StateBehaviour behaviour = state.Behaviours[i];
                    if (behaviour != null)
                        invoke(behaviour, context);
                }
            }
        }
    }
}
