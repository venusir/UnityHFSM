using System;
using UnityEngine;

namespace UnityHFSM.Graph
{
    public static class GraphModelFactory
    {
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>Creates a valid root graph with one default state.</summary>
        public static GraphModel CreateNew()
        {
            var model = new GraphModel
            {
                schemaVersion = 1,
                graphId = NewId(),
            };

            var root = new MachineDef
            {
                id = NewId(),
                name = "Root",
                kind = MachineKind.StateMachine,
            };

            var defaultState = new StateDef
            {
                id = NewId(),
                machineId = root.id,
                name = "State",
                kind = StateKind.State,
                position = new Vector2(0f, 0f),
            };

            root.startStateId = defaultState.id;
            model.rootMachineId = root.id;
            model.machines.Add(root);
            model.states.Add(defaultState);
            return model;
        }

        public static StateDef AddState(
            GraphModel model,
            string machineId,
            string name,
            Vector2 position)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var state = new StateDef
            {
                id = NewId(),
                machineId = machineId,
                name = name,
                kind = StateKind.State,
                position = position,
            };

            model.states.Add(state);
            return state;
        }

        /// <summary>
        /// Turns an existing state into a sub-state-machine host and creates its
        /// child machine with one default state.
        /// </summary>
        public static MachineDef AddSubMachine(GraphModel model, StateDef hostState, string machineName)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (hostState == null)
                throw new ArgumentNullException(nameof(hostState));
            if (hostState.kind == StateKind.HybridSubMachine)
                throw new InvalidOperationException("The state is already a sub-state-machine host.");

            var machine = new MachineDef
            {
                id = NewId(),
                name = machineName,
                parentStateId = hostState.id,
                kind = MachineKind.HybridStateMachine,
            };

            var defaultState = new StateDef
            {
                id = NewId(),
                machineId = machine.id,
                name = "State",
                kind = StateKind.State,
                position = new Vector2(0f, 0f),
            };

            machine.startStateId = defaultState.id;
            hostState.kind = StateKind.HybridSubMachine;
            hostState.subMachineId = machine.id;

            model.machines.Add(machine);
            model.states.Add(defaultState);
            return machine;
        }
    }
}
