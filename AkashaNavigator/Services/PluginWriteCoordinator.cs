namespace AkashaNavigator.Services;

/// <summary>
/// 串行化仓库缓存与已安装插件目录的写操作。
/// </summary>
public sealed class PluginWriteCoordinator
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public IDisposable Acquire()
    {
        _semaphore.Wait();
        return new Releaser(_semaphore);
    }

    public async ValueTask<IDisposable> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}
