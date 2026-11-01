#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Editor-only discovery point for live <see cref="GraphRuntime"/> instances.
    /// Runtimes register automatically on construction and unregister on Dispose,
    /// so tools can inspect pure C# state machines without a MonoBehaviour host.
    /// The whole type is compiled out of player builds.
    /// </summary>
    public static class GraphRuntimeRegistry
    {
        private static readonly object sync = new object();
        private static readonly List<WeakReference<GraphRuntime>> runtimes =
            new List<WeakReference<GraphRuntime>>();

        /// <summary>Raised after a runtime is registered, unregistered, or reset.</summary>
        public static event Action Changed;

        /// <summary>
        /// Fills <paramref name="results"/> with live, non-disposed runtimes and
        /// prunes entries whose targets were collected or disposed.
        /// </summary>
        public static void GetLiveRuntimes(List<GraphRuntime> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();

            lock (sync)
            {
                for (int i = runtimes.Count - 1; i >= 0; i--)
                {
                    if (runtimes[i].TryGetTarget(out GraphRuntime runtime)
                        && runtime != null
                        && !runtime.IsDisposed)
                    {
                        results.Add(runtime);
                    }
                    else
                    {
                        runtimes.RemoveAt(i);
                    }
                }
            }
        }

        internal static void Register(GraphRuntime runtime)
        {
            if (runtime == null || runtime.IsDisposed)
                return;

            bool added = false;

            lock (sync)
            {
                for (int i = 0; i < runtimes.Count; i++)
                {
                    if (runtimes[i].TryGetTarget(out GraphRuntime existing)
                        && ReferenceEquals(existing, runtime))
                    {
                        return;
                    }
                }

                runtimes.Add(new WeakReference<GraphRuntime>(runtime));
                added = true;
            }

            if (added)
                Changed?.Invoke();
        }

        internal static void Unregister(GraphRuntime runtime)
        {
            if (runtime == null)
                return;

            bool removed = false;

            lock (sync)
            {
                for (int i = runtimes.Count - 1; i >= 0; i--)
                {
                    if (!runtimes[i].TryGetTarget(out GraphRuntime existing) || existing == null)
                    {
                        runtimes.RemoveAt(i);
                        continue;
                    }

                    if (ReferenceEquals(existing, runtime))
                    {
                        runtimes.RemoveAt(i);
                        removed = true;
                    }
                }
            }

            if (removed)
                Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            lock (sync)
            {
                runtimes.Clear();
            }

            // Keep subscribers alive when domain reload is disabled.
            Changed?.Invoke();
        }
    }
}
#endif
