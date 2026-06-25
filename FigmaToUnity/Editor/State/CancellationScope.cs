using System;
using System.Threading;

namespace FigmaToUnity.Editor.State
{
    internal sealed class CancellationScope : IDisposable
    {
        private readonly CancellationTokenSource _source = new();

        public CancellationToken Token => _source.Token;
        public bool IsCancellationRequested => _source.IsCancellationRequested;

        public void Cancel()
        {
            if (!_source.IsCancellationRequested)
            {
                _source.Cancel();
            }
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }
}
