using System.Globalization;

using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

[Trait("Category", "Unit")]
public class ServiceTests
{
    // 19/10/2026 10:00 in Portugal (UTC+1 until 25/10/2026).
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly GeoPoint Porto = GeoPoint.Create(41.1579m, -8.6291m);
    private static readonly ServiceCategory Plumbing = Category(3, "Canalização");
    private static readonly User Client = User.Register("Carla Costa", "carla@localhub.test", UserRole.Client);

    // ---------- Create (RF-F009-01, RF-F010-01) ----------

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public void Create_SolicitacoesDiretasComDoisPrestadoresElegiveis_CriaServicoAbertoEDuasSolicitacoesPendentes()
    {
        var service = CreateDirect([Profile(12, Plumbing), Profile(17, Plumbing)]);

        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(ProposalModel.DirectRequest, service.ProposalModel);
        Assert.Same(Client, service.Client);
        Assert.Equal(3, service.ServiceCategoryId);
        Assert.Equal("Torneira com fuga de água.", service.Description);
        Assert.Equal(Today, service.RequestedStartDate);
        Assert.Equal("Rua do Almada 10, Porto", service.Location);
        Assert.Equal(41.1579m, service.Latitude);
        Assert.Equal(-8.6291m, service.Longitude);
        Assert.Equal(Now, service.CreationDate);
        Assert.Null(service.ActualCompletionDate);
        Assert.Equal([12, 17], service.DirectRequests.Select(r => r.ProfessionalProfileId));
        Assert.All(service.DirectRequests, r =>
        {
            Assert.Equal(DirectRequestStatus.Pending, r.Status);
            Assert.Equal(Now, r.CreationDate);
            Assert.Null(r.ResponseDate);
        });
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void Create_TrabalhoPublicado_CriaServicoAbertoSemSolicitacoes()
    {
        var service = Service.Create(Client, Plumbing, ProposalModel.Public, "Substituir torneira.", Today, null,
            "Rua do Almada 10, Porto", Porto, [], Now);

        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(ProposalModel.Public, service.ProposalModel);
        Assert.Null(service.RequestedEndDate);
        Assert.Empty(service.DirectRequests);
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public void Create_MesmoPerfilRepetido_CriaUmaUnicaSolicitacao()
    {
        var profile = Profile(12, Plumbing);

        var service = CreateDirect([profile, profile]);

        Assert.Equal([12], service.DirectRequests.Select(r => r.ProfessionalProfileId));
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void Create_TextosComEspacosNasPontas_GuardaSemEspacos()
    {
        var service = Service.Create(Client, Plumbing, ProposalModel.Public, "  Pintar a sala.  ", Today, null,
            "  Rua do Almada 10, Porto  ", Porto, [], Now);

        Assert.Equal("Pintar a sala.", service.Description);
        Assert.Equal("Rua do Almada 10, Porto", service.Location);
    }

    [Theory]
    [InlineData(UserRole.Provider)]
    [InlineData(UserRole.Admin)]
    [Trait("Requisito", "RF-F009-01")]
    public void Create_UtilizadorQueNaoECliente_LancaErroDeAutorizacao(UserRole role)
    {
        var user = role == UserRole.Admin
            ? User.CreateAdministrator("Ana", "ana@localhub.test")
            : User.Register("Paulo", "paulo@localhub.test", role);

        var exception = Assert.Throws<DomainException>(() => Service.Create(user, Plumbing, ProposalModel.Public,
            "Pintar a sala.", Today, null, "Rua do Almada 10, Porto", Porto, [], Now));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void Create_CategoriaDesativada_LancaErroNaCategoria()
    {
        var inactive = Category(4, "Jardinagem");
        inactive.Deactivate();

        var exception = Assert.Throws<DomainException>(() => Service.Create(Client, inactive, ProposalModel.Public,
            "Cortar a relva.", Today, null, "Rua do Almada 10, Porto", Porto, [], Now));

        AssertValidation(exception, "serviceCategoryId");
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public void Create_PrestadorSemACategoria_LancaErroNosPerfisENaoCriaSolicitacoes()
    {
        var exception = Assert.Throws<DomainException>(() =>
            CreateDirect([Profile(12, Plumbing), Profile(17, Category(8, "Eletricidade"))]));

        AssertValidation(exception, "professionalProfileIds");
        Assert.Contains("perfil 17", exception.Message);
        Assert.DoesNotContain("perfil 12", exception.Message);
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public void Create_SolicitacoesDiretasSemPrestadores_LancaErroNosPerfis()
    {
        var exception = Assert.Throws<DomainException>(() => CreateDirect([]));

        AssertValidation(exception, "professionalProfileIds");
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void Create_TrabalhoPublicadoComPrestadores_LancaErroNosPerfis()
    {
        var exception = Assert.Throws<DomainException>(() => Service.Create(Client, Plumbing, ProposalModel.Public,
            "Pintar a sala.", Today, null, "Rua do Almada 10, Porto", Porto, [Profile(12, Plumbing)], Now));

        AssertValidation(exception, "professionalProfileIds");
    }

    // ---------- Validations used to collect the errors of E20 ----------

    [Theory]
    [InlineData(null)]
    [InlineData((ProposalModel)7)]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateProposalModel_SemValorOuValorDesconhecido_LancaErroNaModalidade(ProposalModel? value)
    {
        var exception = Assert.Throws<DomainException>(() => Service.ValidateProposalModel(value));

        AssertValidation(exception, "proposalModel");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateDescription_ValorLimite_AceitaDe1A1000Caracteres(int length, bool valid)
    {
        AssertLimit(() => Service.ValidateDescription(new string('a', length)), valid, "description");
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateDescription_SoEspacos_LancaErroNaDescricao()
    {
        AssertValidation(Assert.Throws<DomainException>(() => Service.ValidateDescription("   ")), "description");
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateRequestedDates_DataUnicaIgualAHoje_Aceita()
    {
        var (start, end) = Service.ValidateRequestedDates(Today, null, Today);

        Assert.Equal(Today, start);
        Assert.Null(end);
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateRequestedDates_IntervaloComFimIgualAoInicio_Aceita()
    {
        var (start, end) = Service.ValidateRequestedDates(Today.AddDays(2), Today.AddDays(2), Today);

        Assert.Equal(Today.AddDays(2), start);
        Assert.Equal(Today.AddDays(2), end);
    }

    [Theory]
    [InlineData(null, null, "requestedStartDate")]
    [InlineData(-1, null, "requestedStartDate")]
    [InlineData(-1, 3, "requestedStartDate")]
    [InlineData(3, 2, "requestedEndDate")]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateRequestedDates_DatasInvalidas_LancaErroNoCampo(int? startDays, int? endDays, string field)
    {
        DateOnly? start = startDays is null ? null : Today.AddDays(startDays.Value);
        DateOnly? end = endDays is null ? null : Today.AddDays(endDays.Value);

        var exception = Assert.Throws<DomainException>(() => Service.ValidateRequestedDates(start, end, Today));

        AssertValidation(exception, field);
    }

    [Theory]
    [InlineData("2026-10-18T23:30:00Z", "2026-10-19")] // summer time (UTC+1): already the next day in Portugal
    [InlineData("2026-12-31T23:30:00Z", "2026-12-31")] // winter time (UTC+0)
    [Trait("Requisito", "RF-F010-01")]
    public void TodayInPortugal_PertoDaMeiaNoite_DevolveADataDePortugal(string utc, string expected)
    {
        Assert.Equal(
            DateOnly.Parse(expected, CultureInfo.InvariantCulture),
            Service.TodayInPortugal(DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [Trait("Requisito", "RF-F010-01")]
    public void ValidateLocation_ValorLimite_AceitaAte255Caracteres(int length, bool valid)
    {
        AssertLimit(() => Service.ValidateLocation(new string('a', length)), valid, "location");
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public void DescribeIneligible_ContaDesativadaESemCategoria_IdentificaCadaPrestador()
    {
        var deactivated = Profile(12, Plumbing, "Rui Rocha");
        deactivated.ServiceProvider.Deactivate(Now);
        var withoutCategory = Profile(17, Category(8, "Eletricidade"), "Sofia Santos");

        var messages = Service.DescribeIneligible([deactivated, withoutCategory, Profile(20, Plumbing)], 3);

        Assert.Equal(2, messages.Count);
        Assert.Equal("O Prestador Rui Rocha (perfil 12) tem a conta desativada.", messages[0]);
        Assert.Equal("O Prestador Sofia Santos (perfil 17) não tem a categoria do serviço no perfil.", messages[1]);
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public void DescribeIneligible_SemCategoria_SoVerificaAConta()
    {
        var deactivated = Profile(12, Plumbing, "Rui Rocha");
        deactivated.ServiceProvider.Deactivate(Now);

        var messages = Service.DescribeIneligible([deactivated, Profile(17, Category(8, "Eletricidade"))], null);

        Assert.Equal(["O Prestador Rui Rocha (perfil 12) tem a conta desativada."], messages);
    }

    private static Service CreateDirect(IReadOnlyCollection<ProfessionalProfile> recipients) =>
        Service.Create(Client, Plumbing, ProposalModel.DirectRequest, "Torneira com fuga de água.", Today,
            Today.AddDays(3), "Rua do Almada 10, Porto", Porto, recipients, Now);

    // The ids are normally generated by the database, so they are set by reflection.
    private static ServiceCategory Category(int id, string name)
    {
        var category = ServiceCategory.Create(name);
        typeof(ServiceCategory).GetProperty(nameof(ServiceCategory.ServiceCategoryId))!.SetValue(category, id);
        return category;
    }

    private static ProfessionalProfile Profile(int id, ServiceCategory category, string name = "Prestador")
    {
        var provider = User.Register(name, $"prestador{id}@localhub.test", UserRole.Provider);
        var profile = ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [category], 10m);
        typeof(ProfessionalProfile).GetProperty(nameof(ProfessionalProfile.ProfessionalProfileId))!
            .SetValue(profile, id);
        return profile;
    }

    private static void AssertValidation(DomainException exception, string field)
    {
        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal(field, exception.Field);
    }

    private static void AssertLimit(Action validate, bool valid, string field)
    {
        if (valid)
        {
            validate();
            return;
        }

        AssertValidation(Assert.Throws<DomainException>(validate), field);
    }
}