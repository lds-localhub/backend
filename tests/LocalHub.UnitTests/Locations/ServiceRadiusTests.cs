using LocalHub.Domain;
using LocalHub.Domain.Locations;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class ServiceRadiusTests
{
    [Theory]
    [Trait("Requisito", "RF-F015-01")]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    public void Validate_InteiroEntre1E100_DevolveOValor(int km)
    {
        Assert.Equal((short)km, ServiceRadius.Validate(km));
    }

    [Theory]
    [Trait("Requisito", "RF-F015-01")]
    [InlineData(0)]
    [InlineData(150)]
    [InlineData(101)]
    [InlineData(-5)]
    public void Validate_ForaDoIntervalo_LancaErroNoCampo(int km)
    {
        DomainException ex = Assert.Throws<DomainException>(() => ServiceRadius.Validate(km));

        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
        Assert.Equal("serviceRadiusKm", ex.Field);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-01")]
    public void Validate_NaoInteiro_LancaErroNoCampo()
    {
        DomainException ex = Assert.Throws<DomainException>(() => ServiceRadius.Validate(25.5m));

        Assert.Equal("serviceRadiusKm", ex.Field);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-01")]
    public void Validate_SemValor_LancaErroNoCampo()
    {
        DomainException ex = Assert.Throws<DomainException>(() => ServiceRadius.Validate(null));

        Assert.Equal("serviceRadiusKm", ex.Field);
    }
}