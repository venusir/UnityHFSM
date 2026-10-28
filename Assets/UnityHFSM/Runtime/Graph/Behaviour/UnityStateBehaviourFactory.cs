using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Default factory: each runner gets independent behaviour clones so serialized
    /// fields do not leak between runtime instances.
    /// </summary>
    public sealed class UnityStateBehaviourFactory : IStateBehaviourFactory
    {
        public StateBehaviour Create(StateBehaviour asset)
        {
            if (asset == null)
                return null;

            return Object.Instantiate(asset);
        }

        public void Release(StateBehaviour instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(instance);
            else
                Object.DestroyImmediate(instance);
        }
    }
}
