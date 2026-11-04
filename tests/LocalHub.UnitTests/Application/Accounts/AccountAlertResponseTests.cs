using LocalHub.Application.Accounts.Dtos;

namespace LocalHub.UnitTests.Application.Accounts;

// E05: the thresholds of the conduct alerts of the accounts (RF-F002-01, rules 5 to 7).
[Trait("Category", "Unit")]
public class AccountAlertResponseTests
{
    // ---------- ASSIGNED_CANCELLATIONS ----------

    [Theory]
    [Trait("Requisito", "RF-F002-01")]
    [InlineData(4, 5)]
    [InlineData(2, 4)]
    [InlineData(3, 3)]
    public void ForAssignedCancellations_VolumeMinimoELimiarAtingidos_DevolveAlertaComContagemETotal(int count, int total)
    {
        var alert = AccountAlertResponse.ForAssignedCancellations(count, total);

        Assert.NotNull(alert);
        Assert.Equal(AccountAlertType.AssignedCancellations, alert.Type);
        Assert.Equal(count, alert.Count);
        Assert.Equal(total, alert.Total);
        Assert.Null(alert.AverageRating);
        Assert.Null(alert.ReviewCount);
    }

    // 2 of 2 and 0 of 0: fewer than 3 assigned services (volume mínimo).
    [Theory]
    [Trait("Requisito", "RF-F002-01")]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void ForAssignedCancellations_MenosDeTresAtribuidos_NaoDevolveAlerta(int count, int total)
    {
        Assert.Null(AccountAlertResponse.ForAssignedCancellations(count, total));
    }

    // 1 of 4 (25%) and 2 of 5 (40%): below 50%.
    [Theory]
    [Trait("Requisito", "RF-F002-01")]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    public void ForAssignedCancellations_AbaixoDe50PorCento_NaoDevolveAlerta(int count, int total)
    {
        Assert.Null(AccountAlertResponse.ForAssignedCancellations(count, total));
    }

    // ---------- LOW_AVERAGE_RATING ----------

    // Ratings 2, 2 and 1: average 1.7 with 3 reviews; 2.0 is still included.
    [Theory]
    [Trait("Requisito", "RF-F002-01")]
    [InlineData(1.7, 3)]
    [InlineData(2.0, 3)]
    public void ForLowAverageRating_MediaAte2ComTresAvaliacoes_DevolveAlertaComMediaENumero(
        double averageRating, int reviewCount)
    {
        var alert = AccountAlertResponse.ForLowAverageRating((decimal)averageRating, reviewCount);

        Assert.NotNull(alert);
        Assert.Equal(AccountAlertType.LowAverageRating, alert.Type);
        Assert.Equal((decimal)averageRating, alert.AverageRating);
        Assert.Equal(reviewCount, alert.ReviewCount);
        Assert.Null(alert.Count);
        Assert.Null(alert.Total);
    }

    // Ratings 1 and 1: only 2 reviews.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public void ForLowAverageRating_DuasAvaliacoes_NaoDevolveAlerta()
    {
        Assert.Null(AccountAlertResponse.ForLowAverageRating(1.0m, 2));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public void ForLowAverageRating_SemAvaliacoes_NaoDevolveAlerta()
    {
        Assert.Null(AccountAlertResponse.ForLowAverageRating(null, 0));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public void ForLowAverageRating_MediaAcimaDe2_NaoDevolveAlerta()
    {
        Assert.Null(AccountAlertResponse.ForLowAverageRating(2.1m, 3));
    }
}