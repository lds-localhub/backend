using LocalHub.Application.Reports;
using LocalHub.Application.Reports.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Application.Reports;

// E34: the indicators of the activity report of the provider (RF-F014-01) and the months of the period (RF-F014-02).
[Trait("Category", "Unit")]
public class ProviderReportTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);
    private static readonly ReportPeriod NoPeriod = new(null, null);

    // ---------- Indicators (RF-F014-01) ----------

    // Rule 1 and rule 2: only the COMPLETED ones are completed services; the distributions have all the contracted ones.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public void Build_ServicosContratados_ContaOsRealizadosEDistribuiTodos()
    {
        var services = new[]
        {
            Contracted(ServiceStatus.Assigned),
            Contracted(ServiceStatus.Completed),
            Contracted(ServiceStatus.Completed, categoryId: 5, category: "Jardinagem"),
            Contracted(ServiceStatus.Cancelled),
        };

        var report = Build(services);

        Assert.Equal(2, report.CompletedServices);
        Assert.Equal(
            new[] { (ServiceStatus.Assigned, 1), (ServiceStatus.Completed, 2), (ServiceStatus.Cancelled, 1) },
            report.ByStatus.Select(s => (s.Status, s.Count)));
        Assert.Equal(
            new[] { (3, "Canalização", 3), (5, "Jardinagem", 1) },
            report.ByCategory.Select(c => (c.ServiceCategoryId, c.Name, c.Count)));
    }

    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public void Build_SemAtividade_DevolveZerosEListasVazias()
    {
        var report = Build(Array.Empty<ProviderContractedService>());

        Assert.Equal(0, report.CompletedServices);
        Assert.Empty(report.ByCategory);
        Assert.Equal(3, report.ByStatus.Count);
        Assert.All(report.ByStatus, s => Assert.Equal(0, s.Count));
        Assert.Equal(0, report.DirectRequestsReceived);
        Assert.Equal(0, report.Proposals.Accepted);
        Assert.Equal(0, report.Proposals.Declined);
        Assert.Equal(0m, report.TotalAcceptedAmount);
        Assert.Empty(report.RatingEvolution);
    }

    // Criteria 1, 5 and 6: 1 accepted and 3 declined; two direct requests; 80,00 € + 150,00 € = 230,00 €.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public void Build_ContagensRecebidas_ApresentaPropostasSolicitacoesEValor()
    {
        var services = new[] { Contracted(ServiceStatus.Assigned, 80.00m), Contracted(ServiceStatus.Cancelled, 150.00m) };

        var report = Build(services, directRequestsReceived: 2, declinedProposals: 3);

        Assert.Equal(2, report.DirectRequestsReceived);
        Assert.Equal(2, report.Proposals.Accepted);
        Assert.Equal(3, report.Proposals.Declined);
        Assert.Equal(230.00m, report.TotalAcceptedAmount);
    }

    // ---------- Rating evolution (RF-F014-01, rule 5) ----------

    // Criterion 3: each month has the mean of its own reviews, not the accumulated one, and a month without reviews has
    // no value.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public void Build_AvaliacoesEmJulhoESetembro_ApresentaCadaMesAteAoMesAtual()
    {
        var reviews = new[] { Review(2026, 7, 15, 5), Review(2026, 9, 15, 3) };

        var report = Build(reviews: reviews);

        Assert.Equal(
            new[]
            {
                ("2026-07", 1, (decimal?)5.0m),
                ("2026-08", 0, null),
                ("2026-09", 1, 3.0m),
                ("2026-10", 0, null),
            },
            report.RatingEvolution.Select(m => (m.Month, m.ReviewCount, m.AverageRating)));
    }

    // RF-F012-02: one decimal place, 0.05 rounds up. (5 + 4 + 4 + 4) / 4 = 4.25.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public void Build_MediaDoMes_ArredondaComoNoPerfil()
    {
        var reviews = new[]
        {
            Review(2026, 10, 1, 5), Review(2026, 10, 2, 4), Review(2026, 10, 3, 4), Review(2026, 10, 4, 4),
        };

        var month = Assert.Single(Build(reviews: reviews).RatingEvolution);

        Assert.Equal(4, month.ReviewCount);
        Assert.Equal(4.3m, month.AverageRating);
    }

    // The month is the one of the review in UTC (API documentation v04, section 4.3): at 23:30 in UTC, it is already the
    // next month in Portugal in the summer.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public void Build_AvaliacaoNoFimDoMes_UsaOMesEmUtc()
    {
        var reviews = new[] { new ReceivedReview(new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero), 4) };

        var report = Build(reviews: reviews, today: new DateOnly(2026, 9, 30));

        Assert.Equal("2026-09", Assert.Single(report.RatingEvolution).Month);
    }

    // ---------- Rating evolution with a period (RF-F014-02, rule 3) ----------

    // Criterion 3: the reviews of the previous months do not enter in the mean of October.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public void Build_PeriodoDeOutubro_ApresentaApenasOutubro()
    {
        var reviews = new[] { Review(2026, 9, 15, 5), Review(2026, 10, 15, 3) };
        var period = new ReportPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        var month = Assert.Single(Build(reviews: reviews, period: period).RatingEvolution);

        Assert.Equal(("2026-10", 1, (decimal?)3.0m), (month.Month, month.ReviewCount, month.AverageRating));
    }

    // A date in the middle of a month includes that whole month.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public void Build_PeriodoComDatasNoMeioDoMes_IncluiOsMesesDasDuasDatas()
    {
        var reviews = new[] { Review(2026, 7, 1, 5) };
        var period = new ReportPeriod(new DateOnly(2026, 8, 20), new DateOnly(2026, 9, 10));

        var months = Build(reviews: reviews, period: period).RatingEvolution;

        Assert.Equal("2026-08 2026-09", string.Join(' ', months.Select(m => m.Month)));
    }

    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public void Build_SoComDataInicial_VaiAteAoMesAtual()
    {
        var reviews = new[] { Review(2026, 5, 15, 5) };
        var period = new ReportPeriod(new DateOnly(2026, 9, 1), null);

        var months = Build(reviews: reviews, period: period).RatingEvolution;

        Assert.Equal("2026-09 2026-10", string.Join(' ', months.Select(m => m.Month)));
    }

    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public void Build_SoComDataFinal_ComecaNoMesDaPrimeiraAvaliacao()
    {
        var reviews = new[] { Review(2026, 5, 15, 5) };
        var period = new ReportPeriod(null, new DateOnly(2026, 6, 30));

        var months = Build(reviews: reviews, period: period).RatingEvolution;

        Assert.Equal("2026-05 2026-06", string.Join(' ', months.Select(m => m.Month)));
    }

    // The months outside [first review, current month] are not shown, so that very distant dates do not create a list
    // with thousands of months.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public void Build_PeriodoComDatasExtremas_LimitaAosMesesComAtividadePossivel()
    {
        var reviews = new[] { Review(2026, 9, 15, 5) };
        var period = new ReportPeriod(DateOnly.MinValue, DateOnly.MaxValue);

        var months = Build(reviews: reviews, period: period).RatingEvolution;

        Assert.Equal("2026-09 2026-10", string.Join(' ', months.Select(m => m.Month)));
    }

    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public void Build_PeriodoSemAvaliacoesDoPrestador_ListaVazia()
    {
        var period = new ReportPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Empty(Build(period: period).RatingEvolution);
    }

    // ---------- Helpers ----------

    private static ProviderReportResponse Build(
        IReadOnlyCollection<ProviderContractedService>? services = null,
        int directRequestsReceived = 0,
        int declinedProposals = 0,
        IReadOnlyCollection<ReceivedReview>? reviews = null,
        ReportPeriod? period = null,
        DateOnly? today = null)
    {
        return ProviderReport.Build(
            services ?? Array.Empty<ProviderContractedService>(),
            directRequestsReceived,
            declinedProposals,
            reviews ?? Array.Empty<ReceivedReview>(),
            period ?? NoPeriod,
            today ?? Today);
    }

    private static ProviderContractedService Contracted(
        ServiceStatus status, decimal amount = 80.00m, int categoryId = 3, string category = "Canalização")
    {
        return new ProviderContractedService(categoryId, category, status, amount);
    }

    private static ReceivedReview Review(int year, int month, int day, short rating)
    {
        return new ReceivedReview(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero), rating);
    }
}