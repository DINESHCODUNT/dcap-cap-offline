namespace DigitalCap.Core.Interfaces.Service
{
    public interface ITransferProgressStore
    {
        void Set(int projectId, int percent);
        int Get(int projectId);
        void Remove(int projectId);
    }
}
