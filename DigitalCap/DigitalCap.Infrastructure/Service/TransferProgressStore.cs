using System;
using System.Collections.Concurrent;
using DigitalCap.Core.Interfaces.Service;

namespace DigitalCap.Infrastructure.Service
{
    public class TransferProgressStore : ITransferProgressStore
    {
        private readonly ConcurrentDictionary<int, int> _progress = new();

        public void Set(int projectId, int percent)
        {
            _progress[projectId] = Math.Clamp(percent, 0, 100);
        }

        public int Get(int projectId)
        {
            return _progress.TryGetValue(projectId, out var percent) ? percent : 0;
        }

        public void Remove(int projectId)
        {
            _progress.TryRemove(projectId, out _);
        }
    }
}
