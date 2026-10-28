using System;
using System.Collections.Generic;
using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Pure C# runtime core. It owns a built UnityHFSM tree, a blackboard, cloned
    /// behaviours and a deterministic simulation clock. A MonoBehaviour runner is
    /// only a thin host around this class.
    /// </summary>
    public sealed class GraphRuntime : IDisposable
    {
        private readonly List<MachineRuntime> machines = new List<MachineRuntime>();
        private readonly Dictionary<string, MachineRuntime> machineById =
            new Dictionary<string, MachineRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, StateRuntime> stateById =
            new Dictionary<string, StateRuntime>(StringComparer.Ordinal);
        private readonly List<StateBehaviour> behaviourClones = new List<StateBehaviour>();
        private readonly List<TransitionRecord> transitionLog = new List<TransitionRecord>();
        private readonly int transitionLogCapacity;

        private MachineRuntime rootMachine;
        private int transitionSequence;

        internal IStateBehaviourFactory BehaviourFactory;

        public GraphRuntimeOptions Options { get; }
        public BlackboardSchema Schema { get; }
        public Blackboard Blackboard { get; }
        public string GraphId { get; }
        public double Now { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsDisposed { get; private set; }

        public IReadOnlyList<MachineRuntime> Machines => machines;
        public MachineRuntime RootMachine => rootMachine;
        public StateMachine<string, string, string> RootFsm => rootMachine != null ? rootMachine.Fsm : null;
        public IReadOnlyList<TransitionRecord> TransitionLog => transitionLog;

        public GraphRuntime(GraphModel model, GraphRuntimeOptions options = null)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            GraphValidationResult validation = GraphValidator.Validate(model);
            if (validation.HasErrors)
                throw new GraphBuildException(validation);

            Options = options ?? new GraphRuntimeOptions();
            GraphId = model.graphId ?? string.Empty;
            transitionLogCapacity = Math.Max(1, Options.TransitionLogCapacity);
            BehaviourFactory = Options.BehaviourFactory ?? new UnityStateBehaviourFactory();
            Schema = new BlackboardSchema(model.parameters);
            Blackboard = new Blackboard(Schema);

            try
            {
                GraphRuntimeBuilder.Build(this, model);
            }
            catch
            {
                ReleaseBehaviourClones();
                throw;
            }
        }

        public void Start()
        {
            if (IsDisposed || IsStarted || rootMachine == null || rootMachine.Fsm == null)
                return;

            rootMachine.Fsm.Init();
            IsStarted = true;
        }

        public void Tick(float deltaTime)
        {
            if (IsDisposed || !IsStarted || IsPaused)
                return;

            ValidateDelta(deltaTime);
            Now += deltaTime;
            rootMachine.Fsm.OnLogic();
        }

        /// <summary>Advances one logic step even while paused.</summary>
        public void Step(float deltaTime)
        {
            if (IsDisposed || !IsStarted || rootMachine == null)
                return;

            ValidateDelta(deltaTime);
            Now += deltaTime;
            rootMachine.Fsm.OnLogic();
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void SetFloat(string name, float value) => Blackboard.SetFloat(name, value);
        public float GetFloat(string name) => Blackboard.GetFloat(name);
        public void SetInt(string name, int value) => Blackboard.SetInt(name, value);
        public int GetInt(string name) => Blackboard.GetInt(name);
        public void SetBool(string name, bool value) => Blackboard.SetBool(name, value);
        public bool GetBool(string name) => Blackboard.GetBool(name);
        public void SetTrigger(string name) => Blackboard.SetTrigger(name);
        public void ResetTrigger(string name) => Blackboard.ResetTrigger(name);
        public bool GetTrigger(string name) => Blackboard.GetTrigger(name);
        public void ResetAllTriggers() => Blackboard.ResetAllTriggers();

        public void RequestStateChange(string machineId, string stateId, bool forceInstantly = false)
        {
            if (IsDisposed || !IsStarted)
                return;

            if (!machineById.TryGetValue(machineId, out MachineRuntime machine) || !machine.IsInitialized)
                return;

            StateRuntime state = machine.GetState(stateId);
            if (state != null)
                machine.Fsm.RequestStateChange(state.Name, forceInstantly);
        }

        public void RequestExit(string machineId, bool forceInstantly = false)
        {
            if (IsDisposed || !IsStarted)
                return;

            if (machineById.TryGetValue(machineId, out MachineRuntime machine) && machine.IsInitialized)
                machine.Fsm.RequestExit(forceInstantly);
        }

        public double GetStateElapsed(string machineId)
        {
            return !string.IsNullOrEmpty(machineId)
                && machineById.TryGetValue(machineId, out MachineRuntime machine)
                ? machine.StateElapsed
                : 0d;
        }

        public List<MachineRuntime> GetActiveMachinePath()
        {
            var path = new List<MachineRuntime>();
            MachineRuntime current = rootMachine;

            while (current != null)
            {
                path.Add(current);

                if (string.IsNullOrEmpty(current.ActiveStateId))
                    break;

                StateRuntime state = current.GetState(current.ActiveStateId);
                if (state == null || state.ChildMachine == null)
                    break;

                current = state.ChildMachine;
            }

            return path;
        }

        public List<string> GetActiveStateIds()
        {
            List<MachineRuntime> path = GetActiveMachinePath();
            var ids = new List<string>(path.Count);

            for (int i = 0; i < path.Count; i++)
            {
                if (!string.IsNullOrEmpty(path[i].ActiveStateId))
                    ids.Add(path[i].ActiveStateId);
            }

            return ids;
        }

        public GraphRuntimeSnapshot GetSnapshot()
        {
            var snapshot = new GraphRuntimeSnapshot
            {
                time = Now,
                isStarted = IsStarted,
                isPaused = IsPaused,
                graphId = GraphId,
            };

            List<MachineRuntime> activePath = GetActiveMachinePath();
            var activeMachineIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < activePath.Count; i++)
            {
                MachineRuntime machine = activePath[i];
                activeMachineIds.Add(machine.Id);
                snapshot.activePathMachineIds.Add(machine.Id);

                if (!string.IsNullOrEmpty(machine.ActiveStateId))
                    snapshot.activePathStateIds.Add(machine.ActiveStateId);
            }

            for (int i = 0; i < machines.Count; i++)
            {
                MachineRuntime machine = machines[i];
                var machineSnapshot = new MachineRuntimeSnapshot
                {
                    machineId = machine.Id,
                    machineName = machine.Name,
                    isInitialized = machine.IsInitialized,
                    isActive = activeMachineIds.Contains(machine.Id),
                    activeStateId = machine.ActiveStateId,
                    activeStateName = machine.GetStateName(machine.ActiveStateId),
                    stateElapsed = machine.StateElapsed,
                };

                if (machine.IsInitialized && machine.Fsm.HasPendingTransition)
                {
                    machineSnapshot.hasPendingTransition = true;
                    string pendingName = machine.Fsm.PendingStateName;

                    if (string.IsNullOrEmpty(pendingName))
                    {
                        machineSnapshot.isExitPending = true;
                    }
                    else
                    {
                        machineSnapshot.pendingStateName = pendingName;
                        machineSnapshot.pendingStateId = machine.GetStateId(pendingName);
                    }
                }

                snapshot.machines.Add(machineSnapshot);
            }

            snapshot.parameters = Blackboard.Snapshot();
            snapshot.transitions = new List<TransitionRecord>(transitionLog);
            return snapshot;
        }

        internal void RecordTransition(GraphTransition transition)
        {
            if (!Options.EnableDebugTrace)
                return;

            var record = new TransitionRecord
            {
                sequence = ++transitionSequence,
                time = Now,
                machineId = transition.MachineId,
                machineName = transition.MachineName,
                transitionId = transition.TransitionId,
                fromStateId = transition.FromStateId,
                fromStateName = transition.from,
                toStateId = transition.ToStateId,
                toStateName = transition.to,
                isExit = transition.IsExit,
                delaySeconds = transition.DelaySeconds,
            };

            transitionLog.Add(record);
            if (transitionLog.Count > transitionLogCapacity)
                transitionLog.RemoveAt(0);
        }

        internal void RegisterMachine(MachineRuntime machine)
        {
            machines.Add(machine);
            machineById[machine.Id] = machine;
        }

        internal void RegisterState(StateRuntime state)
        {
            stateById[state.Id] = state;
        }

        internal void RegisterBehaviourClone(StateBehaviour behaviour)
        {
            if (behaviour != null)
                behaviourClones.Add(behaviour);
        }

        internal void SetRoot(MachineRuntime root)
        {
            rootMachine = root;
        }

        internal bool TryGetMachine(string machineId, out MachineRuntime machine)
        {
            return machineById.TryGetValue(machineId, out machine);
        }

        internal bool TryGetState(string stateId, out StateRuntime state)
        {
            return stateById.TryGetValue(stateId, out state);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            try
            {
                if (IsStarted && rootMachine != null && rootMachine.Fsm != null)
                    rootMachine.Fsm.OnExit();
            }
            finally
            {
                for (int i = 0; i < machines.Count; i++)
                {
                    MachineRuntime machine = machines[i];
                    if (machine.Fsm != null && machine.StateChangedHandler != null)
                        machine.Fsm.StateChanged -= machine.StateChangedHandler;
                }

                ReleaseBehaviourClones();
                IsStarted = false;
                IsDisposed = true;
            }
        }

        private void ReleaseBehaviourClones()
        {
            for (int i = 0; i < behaviourClones.Count; i++)
            {
                StateBehaviour behaviour = behaviourClones[i];
                if (behaviour != null && BehaviourFactory != null)
                    BehaviourFactory.Release(behaviour);
            }

            behaviourClones.Clear();
        }

        private static void ValidateDelta(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and non-negative.");
        }
    }
}
