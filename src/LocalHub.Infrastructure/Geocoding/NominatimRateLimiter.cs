using Microsoft.Extensions.Options;

namespace LocalHub.Infrastructure.Geocoding;

public sealed class NominatimRateLimiter(IOptions<NominatimOptions> options, TimeProvider timeProvider) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minInterval = options.Value.MinIntervalBetweenRequests;
    private DateTimeOffset? _lastRequestAt;

    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_lastRequestAt is { } last)
            {
                TimeSpan wait = last + _minInterval - timeProvider.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }

            _lastRequestAt = timeProvider.GetUtcNow();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}