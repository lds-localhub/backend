using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

// E24 and E25: the proposals and the answers to the direct requests (sections 10.3 and 10.4 of the requirements).
[Trait("Category", "Unit")]
public class ServiceProposalTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly GeoPoint Porto = GeoPoint.Create(41.1579m, -8.6291m);
    private static readonly ServiceCategory Plumbing = Category(3, "Canalização");
    private static readonly ServiceCategory Painting = Category(4, "Pintura");

    // ---------- SubmitProposal: direct request (RF-F007-01) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void SubmitProposal_SolicitacaoPendente_CriaPropostaPendenteEMarcaAPropostaApresentada()
    {
        var provider = Profile(12, Plumbing);
        var service = CreateDirect(NewClient(), [provider, Profile(17, Plumbing)]);

        var proposal = service.SubmitProposal(provider, 80.00m, " Inclui deslocação. ", "Disponível dia 9.", Later);

        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Equal(80.00m, proposal.Amount);
        Assert.Equal("Inclui deslocação.", proposal.Conditions);
        Assert.Equal("Disponível dia 9.", proposal.Obs);
        Assert.Equal(12, proposal.ProfessionalProfileId);
        Assert.Equal(Later, proposal.CreationDate);
        Assert.Null(proposal.DecisionDate);
        Assert.Same(proposal, Assert.Single(service.Proposals));

        var answered = service.DirectRequests.Single(r => r.ProfessionalProfileId == 12);
        Assert.Equal(DirectRequestStatus.ProposalSubmitted, answered.Status);
        Assert.Equal(Later, answered.ResponseDate);
        var other = service.DirectRequests.Single(r => r.ProfessionalProfileId == 17);
        Assert.Equal(DirectRequestStatus.Pending, other.Status);
        Assert.Equal(ServiceStatus.Open, service.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void SubmitProposal_SemObservacoes_CriaPropostaSemObservacoes()
    {
        var provider = Profile(12, Plumbing);
        var service = CreateDirect(NewClient(), [provider]);

        var proposal = service.SubmitProposal(provider, 80m, "Material incluído.", null, Later);

        Assert.Null(proposal.Obs);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void SubmitProposal_PrestadorSemSolicitacao_LancaForbidden()
    {
        var service = CreateDirect(NewClient(), [Profile(12, Plumbing)]);

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(Profile(99, Plumbing), 80m, "Condições.", null, Later));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Empty(service.Proposals);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void SubmitProposal_SolicitacaoRecusada_LancaInvalidTransition()
    {
        var provider = Profile(12, Plumbing);
        var service = CreateDirect(NewClient(), [provider]);
        service.DeclineDirectRequest(0, 12, Now);

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(provider, 80m, "Condições.", null, Later));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Empty(service.Proposals);
    }

    // ---------- SubmitProposal: published job (RF-F011-02) ----------

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public void SubmitProposal_TrabalhoPublicadoComACategoria_CriaPropostaPendente()
    {
        var provider = Profile(12, Plumbing);
        var service = CreatePublic(NewClient());

        var proposal = service.SubmitProposal(provider, 150m, "Material incluído.", null, Later);

        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Empty(service.DirectRequests);
        Assert.Same(proposal, Assert.Single(service.Proposals));
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public void SubmitProposal_PrestadorSemACategoria_LancaForbidden()
    {
        var service = CreatePublic(NewClient());

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(Profile(12, Painting), 150m, "Condições.", null, Later));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public void SubmitProposal_ClienteComContaDesativada_LancaForbidden()
    {
        var client = NewClient();
        var service = CreatePublic(client);
        client.Deactivate(Later);

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(Profile(12, Plumbing), 150m, "Condições.", null, Later.AddHours(1)));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
    }

    [Theory]
    [InlineData(ServiceStatus.Assigned)]
    [InlineData(ServiceStatus.Completed)]
    [InlineData(ServiceStatus.Cancelled)]
    [Trait("Requisito", "RF-F011-02")]
    public void SubmitProposal_ServicoNaoAberto_LancaInvalidTransition(ServiceStatus status)
    {
        var service = CreatePublic(NewClient());
        typeof(Service).GetProperty(nameof(Service.Status))!.SetValue(service, status);

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(Profile(12, Plumbing), 150m, "Condições.", null, Later));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public void SubmitProposal_SegundaPropostaDoMesmoPrestador_LancaConflict()
    {
        var provider = Profile(12, Plumbing);
        var service = CreatePublic(NewClient());
        service.SubmitProposal(provider, 150m, "Condições.", null, Later);

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(provider, 120m, "Outras condições.", null, Later.AddHours(1)));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Single(service.Proposals);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public void SubmitProposal_SegundaPropostaNaSolicitacaoDireta_LancaConflict()
    {
        var provider = Profile(12, Plumbing);
        var service = CreateDirect(NewClient(), [provider]);
        service.SubmitProposal(provider, 80m, "Condições.", null, Later);

        var exception = Assert.Throws<DomainException>(
            () => service.SubmitProposal(provider, 70m, "Condições.", null, Later.AddHours(1)));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
    }

    // ---------- DeclineDirectRequest (RF-F007-02, RF-F007-08) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public void DeclineDirectRequest_SolicitacaoPendente_FicaRecusadaEOServicoMantemSeAberto()
    {
        var service = CreateDirect(NewClient(), [Profile(12, Plumbing)]);

        var request = service.DeclineDirectRequest(0, 12, Later);

        Assert.Equal(DirectRequestStatus.Declined, request.Status);
        Assert.Equal(Later, request.ResponseDate);
        Assert.Equal(ServiceStatus.Open, service.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public void DeclineDirectRequest_OutroPrestador_LancaForbidden()
    {
        var service = CreateDirect(NewClient(), [Profile(12, Plumbing)]);

        var exception = Assert.Throws<DomainException>(() => service.DeclineDirectRequest(0, 99, Later));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(DirectRequestStatus.Pending, service.DirectRequests.Single().Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-08")]
    public void DeclineDirectRequest_SolicitacaoJaRecusada_LancaInvalidTransition()
    {
        var service = CreateDirect(NewClient(), [Profile(12, Plumbing)]);
        service.DeclineDirectRequest(0, 12, Later);

        var exception = Assert.Throws<DomainException>(() => service.DeclineDirectRequest(0, 12, Later.AddHours(1)));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(Later, service.DirectRequests.Single().ResponseDate);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-08")]
    public void DeclineDirectRequest_PropostaJaApresentada_LancaInvalidTransition()
    {
        var provider = Profile(12, Plumbing);
        var service = CreateDirect(NewClient(), [provider]);
        service.SubmitProposal(provider, 80m, "Condições.", null, Later);

        var exception = Assert.Throws<DomainException>(() => service.DeclineDirectRequest(0, 12, Later.AddHours(1)));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public void DeclineDirectRequest_ServicoNaoAberto_LancaInvalidTransition()
    {
        var service = CreateDirect(NewClient(), [Profile(12, Plumbing)]);
        typeof(Service).GetProperty(nameof(Service.Status))!.SetValue(service, ServiceStatus.Cancelled);

        var exception = Assert.Throws<DomainException>(() => service.DeclineDirectRequest(0, 12, Later));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public void DeclineDirectRequest_SolicitacaoDeOutroServico_LancaConflict()
    {
        var service = CreateDirect(NewClient(), [Profile(12, Plumbing)]);

        var exception = Assert.Throws<DomainException>(() => service.DeclineDirectRequest(777, 12, Later));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
    }

    // ---------- Helpers ----------

    private static User NewClient() => User.Register("Carla Costa", "carla@localhub.test", UserRole.Client);

    private static Service CreateDirect(User client, IReadOnlyCollection<ProfessionalProfile> recipients) =>
        Service.Create(client, Plumbing, ProposalModel.DirectRequest, "Reparar torneira", "Torneira com fuga de água.",
            Today, null, "Rua do Almada 10, Porto", Porto.Latitude, Porto.Longitude, recipients, Now);

    private static Service CreatePublic(User client) =>
        Service.Create(client, Plumbing, ProposalModel.Public, "Reparar torneira", "Substituir torneira.", Today, null,
            "Rua do Almada 10, Porto", Porto.Latitude, Porto.Longitude, [], Now);

    // The ids are normally generated by the database, so they are set by reflection.
    private static ServiceCategory Category(int id, string name)
    {
        var category = ServiceCategory.Create(name);
        typeof(ServiceCategory).GetProperty(nameof(ServiceCategory.ServiceCategoryId))!.SetValue(category, id);
        return category;
    }

    private static ProfessionalProfile Profile(int id, ServiceCategory category)
    {
        var provider = User.Register("Prestador", $"prestador{id}@localhub.test", UserRole.Provider);
        var profile = ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [category], 10);
        typeof(ProfessionalProfile).GetProperty(nameof(ProfessionalProfile.ProfessionalProfileId))!
            .SetValue(profile, id);
        return profile;
    }
}