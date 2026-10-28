using System;
using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// MonoBehaviour host for a GraphRuntime. Code-first users do not need this;
    /// it is the optional binding between a graph asset and the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StateMachineRunner : MonoBehaviour
    {
        [SerializeField]
        private StateMachineGraphAsset graph;

        [SerializeField]
        private RunnerUpdateMode updateMode = RunnerUpdateMode.Update;

        [SerializeField]
        private bool playOnEnable = true;

        private bool startedOnce;

        public StateMachineGraphAsset Graph
        {
            get => graph;
            set
            {
                if (graph == value)
                    return;

                graph = value;
                if (Application.isPlaying)
                    Rebuild();
            }
        }

        public RunnerUpdateMode UpdateMode
        {
            get => updateMode;
            set => updateMode = value;
        }

        public GraphRuntime Runtime { get; private set; }
        public Blackboard Blackboard => Runtime != null ? Runtime.Blackboard : null;
        public bool IsPaused => Runtime != null && Runtime.IsPaused;

        private void OnEnable()
        {
            if (Runtime == null)
                BuildRuntime();

            StateMachineRunnerRegistry.Register(this);

            if (startedOnce && playOnEnable)
                StartMachine();
        }

        private void Start()
        {
            if (playOnEnable)
                StartMachine();

            startedOnce = true;
        }

        private void OnDisable()
        {
            StateMachineRunnerRegistry.Unregister(this);
            DisposeRuntime();
        }

        private void OnDestroy()
        {
            DisposeRuntime();
        }

        private void Update()
        {
            if (updateMode == RunnerUpdateMode.Update && Runtime != null)
                Runtime.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (updateMode == RunnerUpdateMode.FixedUpdate && Runtime != null)
                Runtime.Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            Runtime?.Tick(deltaTime);
        }

        public void Step(float deltaTime)
        {
            Runtime?.Step(deltaTime);
        }

        public void Pause()
        {
            Runtime?.Pause();
        }

        public void Resume()
        {
            Runtime?.Resume();
        }

        public void StartMachine()
        {
            if (Runtime == null)
                BuildRuntime();

            Runtime?.Start();
            startedOnce = true;
        }

        public void Rebuild(bool preserveBlackboard = true)
        {
            if (graph == null)
            {
                DisposeRuntime();
                return;
            }

            GraphRuntime previous = Runtime;
            GraphRuntime next;

            try
            {
                next = CreateRuntime();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            if (preserveBlackboard && previous != null)
                next.Blackboard.CopyValuesFrom(previous.Blackboard);

            bool shouldStart = previous != null && previous.IsStarted;

            Runtime = next;

            if (shouldStart || playOnEnable)
                next.Start();

            previous?.Dispose();
        }

        public void SetFloat(string name, float value) => Runtime?.SetFloat(name, value);
        public float GetFloat(string name) => Runtime != null ? Runtime.GetFloat(name) : 0f;
        public void SetInt(string name, int value) => Runtime?.SetInt(name, value);
        public int GetInt(string name) => Runtime != null ? Runtime.GetInt(name) : 0;
        public void SetBool(string name, bool value) => Runtime?.SetBool(name, value);
        public bool GetBool(string name) => Runtime != null && Runtime.GetBool(name);
        public void SetTrigger(string name) => Runtime?.SetTrigger(name);
        public void ResetTrigger(string name) => Runtime?.ResetTrigger(name);
        public bool GetTrigger(string name) => Runtime != null && Runtime.GetTrigger(name);
        public void ResetAllTriggers() => Runtime?.ResetAllTriggers();
        public void RequestStateChange(string machineId, string stateId, bool forceInstantly = false) =>
            Runtime?.RequestStateChange(machineId, stateId, forceInstantly);
        public void RequestExit(string machineId, bool forceInstantly = false) =>
            Runtime?.RequestExit(machineId, forceInstantly);

        private void BuildRuntime()
        {
            if (graph == null)
                return;

            Runtime = CreateRuntime();
        }

        private GraphRuntime CreateRuntime()
        {
            return new GraphRuntime(
                graph.Graph,
                new GraphRuntimeOptions
                {
                    Host = gameObject,
                });
        }

        private void DisposeRuntime()
        {
            if (Runtime == null)
                return;

            Runtime.Dispose();
            Runtime = null;
        }
    }
}
