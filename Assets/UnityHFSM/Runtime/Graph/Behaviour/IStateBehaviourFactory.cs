namespace UnityHFSM.Graph
{
    public interface IStateBehaviourFactory
    {
        StateBehaviour Create(StateBehaviour asset);
        void Release(StateBehaviour instance);
    }
}
