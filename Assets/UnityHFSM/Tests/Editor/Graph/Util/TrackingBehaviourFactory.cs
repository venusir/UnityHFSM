using UnityHFSM.Graph;

namespace UnityHFSM.Graph.Tests
{
    public sealed class TrackingBehaviourFactory : IStateBehaviourFactory
    {
        private readonly IStateBehaviourFactory inner = new UnityStateBehaviourFactory();

        public int Created;
        public int Released;

        public StateBehaviour Create(StateBehaviour asset)
        {
            Created++;
            return inner.Create(asset);
        }

        public void Release(StateBehaviour instance)
        {
            Released++;
            inner.Release(instance);
        }
    }
}
