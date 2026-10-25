using System.Globalization;

using LocalHub.Domain.Common;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

[Trait("Category", "Unit")]
public class ProposalTests
{
    // ---------- Amount (RF-F007-01 and RF-F011-02, rules 2 and 3) ----------

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("12.345")]
    [InlineData("100000.01")]
    [Trait("Requisito", "RF-F007-01")]
    [Trait("Requisito", "RF-F011-02")]
    public void ValidateAmount_ValorInvalido_LancaErroNoCampoAmount(string value)
    {
        decimal amount = decimal.Parse(value, CultureInfo.InvariantCulture);

        var exception = Assert.Throws<DomainException>(() => Proposal.ValidateAmount(amount));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal(Proposal.AmountField, exception.Field);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void ValidateAmount_SemValor_LancaErroNoCampoAmount()
    {
        var exception = Assert.Throws<DomainException>(() => Proposal.ValidateAmount(null));

        Assert.Equal(Proposal.AmountField, exception.Field);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("80.00")]
    [InlineData("12.5")]
    [InlineData("100000.00")]
    [Trait("Requisito", "RF-F007-01")]
    public void ValidateAmount_ValorValido_DevolveOValor(string value)
    {
        decimal amount = decimal.Parse(value, CultureInfo.InvariantCulture);

        Assert.Equal(amount, Proposal.ValidateAmount(amount));
    }

    // ---------- Conditions (rule 2) ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1001")]
    [Trait("Requisito", "RF-F007-01")]
    [Trait("Requisito", "RF-F011-02")]
    public void ValidateConditions_CondicoesEmFaltaOuLongas_LancaErroNoCampoConditions(string? value)
    {
        // "1001": a text with that number of characters.
        string? conditions = value is not null && int.TryParse(value, out int length) ? new string('a', length) : value;

        var exception = Assert.Throws<DomainException>(() => Proposal.ValidateConditions(conditions));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal(Proposal.ConditionsField, exception.Field);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void ValidateConditions_MilCaracteres_DevolveOTexto()
    {
        string conditions = new('a', Proposal.ConditionsMaxLength);

        Assert.Equal(conditions, Proposal.ValidateConditions(conditions));
    }

    // ---------- Obs (rule 2: optional, at most 500 characters) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    [Trait("Requisito", "RF-F011-02")]
    public void ValidateObs_MaisDeQuinhentosCaracteres_LancaErroNoCampoObs()
    {
        string obs = new('a', Proposal.ObsMaxLength + 1);

        var exception = Assert.Throws<DomainException>(() => Proposal.ValidateObs(obs));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal(Proposal.ObsField, exception.Field);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void ValidateObs_QuinhentosCaracteres_DevolveOTexto()
    {
        string obs = new('a', Proposal.ObsMaxLength);

        Assert.Equal(obs, Proposal.ValidateObs(obs));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requisito", "RF-F007-01")]
    public void ValidateObs_SemObservacoes_DevolveNulo(string? obs)
    {
        Assert.Null(Proposal.ValidateObs(obs));
    }
}