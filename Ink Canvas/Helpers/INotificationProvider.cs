using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    internal interface INotificationProvider : IDisposable
    {
        string ProviderId { get; }
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync();
    }
}
