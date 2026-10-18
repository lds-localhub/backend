using LocalHub.Infrastructure.Geocoding;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LocalHub.UnitTests.Locations;

/// <summary>Política de utilização do Nominatim: no máximo um pedido por segundo (critério de aceitação 6 da I043).</summary>
[Trait("Category", "Unit")]
public class NominatimRateLimiterTests
{
    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task WaitTurnAsync_PrimeiroPedido_NaoEspera()
    {
        using NominatimRateLimiter limiter = CreateLimiter(new FakeTimeProvider());

        Task turn = limiter.WaitTurnAsync(CancellationToken.None);

        Assert.True(turn.IsCompleted);
        await turn;
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task WaitTurnAsync_SegundoPedidoNoMesmoSegundo_EsperaAteCompletarUmSegundo()
    {
        FakeTimeProvider time = new();
        using NominatimRateLimiter limiter = CreateLimiter(time);
        await limiter.WaitTurnAsync(CancellationToken.None);

        Task second = limiter.WaitTurnAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(second.IsCompletedSuccessfully);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task WaitTurnAsync_PedidoDepoisDeUmSegundo_NaoEspera()
    {
        FakeTimeProvider time = new();
        using NominatimRateLimiter limiter = CreateLimiter(time);
        await limiter.WaitTurnAsync(CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(2));
        Task next = limiter.WaitTurnAsync(CancellationToken.None);

        Assert.True(next.IsCompleted);
        await next;
    }

    private static NominatimRateLimiter CreateLimiter(TimeProvider time)
    {
        return new NominatimRateLimiter(Options.Create(new NominatimOptions { UserAgent = "LocalHub-Testes/1.0" }), time);
    }
}