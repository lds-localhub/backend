using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Reports;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Application.Reports;

// E33: the indicators of the activity report of the client (RF-F013-01) and its period (RF-F013-02).
[Trait("Category", "Unit")]
public class ClientReportTests
{
    private static readonly ReportPeriod AllActivity = new(null, null);
    private static readonly DateTimeOffset Completion = new(2026, 10, 9, 18, 40, 0, TimeSpan.Zero);

    // ---------- Indicators (RF-F013-01) ----------

    // Criterion 1: one ASSIGNED, one COMPLETED and one CANCELLED.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public void Build_TresServicosContratados_ContaUmEmCadaEstado()
    {
        var services = new[]
        {
            Contracted(1, ServiceStatus.Assigned),
            Contracted(2, ServiceStatus.Completed, completion: Completion),
            Contracted(3, ServiceStatus.Cancelled),
        };

        var report = ClientReport.Build(AllActivity, services);

        Assert.Equal(3, report.ContractedServices);
        Assert.Equal(
            new[] { (ServiceStatus.Assigned, 1), (ServiceStatus.Completed, 1), (ServiceStatus.Cancelled, 1) },
            report.ByStatus.Select(s => (s.Status, s.Count)));
    }

    // Rule 2: the three states are always shown, also with 0.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public void Build_SemServicos_DevolveZerosEListasVazias()
    {
        var report = ClientReport.Build(AllActivity, Array.Empty<ContractedService>());

        Assert.Equal(0, report.ContractedServices);
        Assert.Empty(report.ByCategory);
        Assert.All(report.ByStatus, s => Assert.Equal(0, s.Count));
        Assert.Equal(3, report.ByStatus.Count);
        Assert.Equal(0m, report.TotalAcceptedAmount);
        Assert.Empty(report.CompletedHistory);
    }

    // Criterion 5: two of «Canalização» and one of «Jardinagem», the most frequent first.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public void Build_DoisDeCanalizacaoEUmDeJardinagem_DistribuiPorCategoria()
    {
        var services = new[]
        {
            Contracted(1, ServiceStatus.Assigned, categoryId: 5, category: "Jardinagem"),
            Contracted(2, ServiceStatus.Assigned),
            Contracted(3, ServiceStatus.Cancelled),
        };

        var report = ClientReport.Build(AllActivity, services);

        Assert.Equal(
            new[] { (3, "Canalização", 2), (5, "Jardinagem", 1) },
            report.ByCategory.Select(c => (c.ServiceCategoryId, c.Name, c.Count)));
    }

    // Criterion 2: 80,00 € + 150,00 € = 230,00 €, also when the service was cancelled after being assigned.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public void Build_PropostasAceitesDe80E150_SomaOValorTotal()
    {
        var services = new[]
        {
            Contracted(1, ServiceStatus.Completed, amount: 80.00m, completion: Completion),
            Contracted(2, ServiceStatus.Cancelled, amount: 150.00m),
        };

        var report = ClientReport.Build(AllActivity, services);

        Assert.Equal(230.00m, report.TotalAcceptedAmount);
    }

    // Criterion 3 and rule 4: only the COMPLETED services, with the date, the category, the provider and the value.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public void Build_HistoricoTemSoOsRealizadosDoMaisRecenteParaOMaisAntigo()
    {
        var older = Completion.AddDays(-3);
        var services = new[]
        {
            Contracted(1, ServiceStatus.Completed, amount: 80.00m, completion: older),
            Contracted(2, ServiceStatus.Assigned),
            Contracted(3, ServiceStatus.Completed, amount: 120.50m, completion: Completion, provider: "Águas & Companhia"),
            Contracted(4, ServiceStatus.Cancelled),
        };

        var report = ClientReport.Build(AllActivity, services);

        Assert.Collection(report.CompletedHistory,
            first =>
            {
                Assert.Equal(3, first.ServiceId);
                Assert.Equal("Canalização", first.ServiceCategory);
                Assert.Equal("Águas & Companhia", first.Provider);
                Assert.Equal(120.50m, first.Amount);
                Assert.Equal(Completion, first.ActualCompletionDate);
            },
            second => Assert.Equal(1, second.ServiceId));
    }

    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Build_DevolveOPeriodoPedido()
    {
        var period = new ReportPeriod(new DateOnly(2026, 10, 1), null);

        var report = ClientReport.Build(period, Array.Empty<ContractedService>());

        Assert.Equal(new DateOnly(2026, 10, 1), report.Period.StartDate);
        Assert.Null(report.Period.EndDate);
    }

    // ---------- Period (RF-F013-02) ----------

    // Rule 4: without period, all the activity.
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Parse_SemDatas_NaoLimitaOPeriodo()
    {
        var period = ReportPeriod.Parse(null, "");

        Assert.Null(period.StartDate);
        Assert.Null(period.EndDate);
        Assert.Null(period.From);
        Assert.Null(period.Until);
    }

    // Rules 1 and 2: both dates included, in the time of Portugal (UTC+1 on 1 October, UTC+0 on 1 November).
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Parse_Outubro_IncluiODiaInteiroDeAmbasAsDatasEmPortugal()
    {
        var period = ReportPeriod.Parse("2026-10-01", "2026-10-31");

        Assert.Equal(new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero), period.From);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), period.Until);
        Assert.Equal(TimeSpan.Zero, period.From!.Value.Offset);
    }

    // The same day as start and end is a valid period.
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Parse_MesmoDia_EValido()
    {
        var period = ReportPeriod.Parse("2026-10-15", "2026-10-15");

        Assert.Equal(TimeSpan.FromDays(1), period.Until - period.From);
    }

    // Rule 3 (criterion 2).
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Parse_DataInicialPosteriorAFinal_LancaErroGlobal()
    {
        var exception = Assert.Throws<ValidationException>(() => ReportPeriod.Parse("2026-10-31", "2026-10-01"));

        Assert.Equal(ReportPeriod.InvertedMessage, exception.Message);
        Assert.Empty(exception.Errors);
    }

    [Theory]
    [Trait("Requisito", "RF-F013-02")]
    [InlineData("2026-13-01", null)]
    [InlineData("01/10/2026", null)]
    [InlineData(null, "2026-02-30")]
    [InlineData(null, "ontem")]
    public void Parse_DataInvalida_LancaErroGlobal(string? startDate, string? endDate)
    {
        var exception = Assert.Throws<ValidationException>(() => ReportPeriod.Parse(startDate, endDate));

        Assert.Equal(ReportPeriod.InvalidDateMessage, exception.Message);
        Assert.Empty(exception.Errors);
    }

    // ---------- Helpers ----------

    private static ContractedService Contracted(
        int serviceId,
        ServiceStatus status,
        decimal amount = 80.00m,
        DateTimeOffset? completion = null,
        int categoryId = 3,
        string category = "Canalização",
        string provider = "Canalizações Silva") =>
        new(serviceId, categoryId, category, status, completion, amount, provider);
}