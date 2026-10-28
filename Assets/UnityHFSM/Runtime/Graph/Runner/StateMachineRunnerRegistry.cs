using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Lightweight registry used by the editor window to find live runners.
    /// The editor also scans the scene, so a stale registry is not fatal.
    /// </summary>
    public static class StateMachineRunnerRegistry
    {
        private static readonly List<StateMachineRunner> runners = new List<StateMachineRunner>();

        public static IReadOnlyList<StateMachineRunner> Runners => runners;
        public static event Action Changed;

        internal static void Register(StateMachineRunner runner)
        {
            if (runner == null || runners.Contains(runner))
                return;

            runners.Add(runner);
            Changed?.Invoke();
        }

        internal static void Unregister(StateMachineRunner runner)
        {
            if (runner != null && runners.Remove(runner))
                Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            runners.Clear();
            Changed = null;
        }
    }
}
