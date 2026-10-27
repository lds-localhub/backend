using Microsoft.Extensions.Options;

namespace LocalHub.Infrastructure.Geocoding;

public sealed class NominatimRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minInterval;
    private DateTimeOffset? _lastRequestAt;

    private readonly TimeProvider _timeProvider;

    public NominatimRateLimiter(IOptions<NominatimOptions> options, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _minInterval = options.Value.MinIntervalBetweenRequests;
    }

    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_lastRequestAt is { } last)
            {
                TimeSpan wait = last + _minInterval - _timeProvider.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, _timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }

            _lastRequestAt = _timeProvider.GetUtcNow();
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