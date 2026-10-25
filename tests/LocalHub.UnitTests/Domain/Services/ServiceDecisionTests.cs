using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

// E28 and E29: the decisions of the client on the proposals (sections 10.1 to 10.4 of the requirements).
[Trait("Category", "Unit")]
public class ServiceDecisionTests
{
    private const int ClientId = 45;
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);
    private static readonly DateTimeOffset Decision = Now.AddHours(5);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly GeoPoint Porto = GeoPoint.Create(41.1579m, -8.6291m);
    private static readonly ServiceCategory Plumbing = Category(3, "Canalização");

    // ---------- AcceptProposal (RF-F007-03, RF-F008-02) ----------

    // CA1 of I048: three PENDING proposals, one accepted.
    [Fact]
    [Trait("Requisito", "RF-F007-03")]
    public void AcceptProposal_TresPropostasPendentes_AtribuiOServicoERecusaAsOutras()
    {
        var first = Profile(12);
        var second = Profile(17);
        var third = Profile(21);
        var silent = Profile(30);
        var service = CreateDirect([first, second, third, silent]);
        var accepted = Submit(service, first, 101);
        var declined1 = Submit(service, second, 102);
        var declined2 = Submit(service, third, 103);

        var result = service.AcceptProposal(101, ClientId, Decision);

        Assert.Same(accepted, result);
        Assert.Equal(ServiceStatus.Assigned, service.Status);
        Assert.Equal(ProposalStatus.Accepted, accepted.Status);
        Assert.Equal(Decision, accepted.DecisionDate);
        Assert.All([declined1, declined2], p =>
        {
            Assert.Equal(ProposalStatus.Declined, p.Status);
            Assert.Equal(Decision, p.DecisionDate);
        });

        // The provider that did not answer gets VOID, without responseDate; the others keep PROPOSAL_SUBMITTED.
        var voided = service.DirectRequests.Single(r => r.ProfessionalProfileId == 30);
        Assert.Equal(DirectRequestStatus.Void, voided.Status);
        Assert.Null(voided.ResponseDate);
        Assert.All(service.DirectRequests.Where(r => r.ProfessionalProfileId != 30),
            r => Assert.Equal(DirectRequestStatus.ProposalSubmitted, r.Status));
    }

    [Fact]
    [Trait("Requisito", "RF-F008-02")]
    public void AcceptProposal_TrabalhoPublicado_AtribuiOServico()
    {
        var provider = Profile(12);
        var service = CreatePublic();
        var proposal = Submit(service, provider, 101);

        service.AcceptProposal(101, ClientId, Decision);

        Assert.Equal(ServiceStatus.Assigned, service.Status);
        Assert.Equal(ProposalStatus.Accepted, proposal.Status);
    }

    // CA2: a second acceptance in the same service.
    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    public void AcceptProposal_ServicoJaAtribuido_LancaInvalidTransitionSemAlterarAProposta()
    {
        var service = CreatePublic();
        Submit(service, Profile(12), 101);
        var other = Submit(service, Profile(17), 102);
        service.AcceptProposal(101, ClientId, Decision);

        var exception = Assert.Throws<DomainException>(() => service.AcceptProposal(102, ClientId, Decision));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(ProposalStatus.Declined, other.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-06")]
    public void AcceptProposal_PropostaRecusada_LancaInvalidTransition()
    {
        var service = CreatePublic();
        var proposal = Submit(service, Profile(12), 101);
        service.DeclineProposal(101, ClientId, Decision);

        var exception = Assert.Throws<DomainException>(() => service.AcceptProposal(101, ClientId, Decision));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(ProposalStatus.Declined, proposal.Status);
    }

    // CA2: RF-F002-02, rule 7.
    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public void AcceptProposal_PrestadorComContaDesativada_LancaConflictEOServicoFicaAberto()
    {
        var provider = Profile(12);
        var service = CreatePublic();
        var proposal = Submit(service, provider, 101);
        provider.ServiceProvider.Deactivate(Later);

        var exception = Assert.Throws<DomainException>(() => service.AcceptProposal(101, ClientId, Decision));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
    }

    // CA4: only the client that created the service decides.
    [Fact]
    [Trait("Requisito", "RF-F007-08")]
    public void AcceptProposal_OutroCliente_LancaForbidden()
    {
        var service = CreatePublic();
        Submit(service, Profile(12), 101);

        var exception = Assert.Throws<DomainException>(() => service.AcceptProposal(101, 99, Decision));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ServiceStatus.Open, service.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-03")]
    public void AcceptProposal_PropostaDeOutroServico_LancaConflict()
    {
        var service = CreatePublic();

        var exception = Assert.Throws<DomainException>(() => service.AcceptProposal(777, ClientId, Decision));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
    }

    // ---------- DeclineProposal (RF-F007-04, RF-F008-03) ----------

    // CA3: only that proposal changes.
    [Fact]
    [Trait("Requisito", "RF-F007-04")]
    public void DeclineProposal_PropostaPendente_FicaRecusadaEORestoNaoMuda()
    {
        var silent = Profile(30);
        var service = CreateDirect([Profile(12), Profile(17), silent]);
        var declined = Submit(service, service.DirectRequests.First().ProfessionalProfile, 101);
        var other = Submit(service, service.DirectRequests.Skip(1).First().ProfessionalProfile, 102);

        var result = service.DeclineProposal(101, ClientId, Decision);

        Assert.Same(declined, result);
        Assert.Equal(ProposalStatus.Declined, declined.Status);
        Assert.Equal(Decision, declined.DecisionDate);
        Assert.Equal(ProposalStatus.Pending, other.Status);
        Assert.Equal(DirectRequestStatus.Pending, service.DirectRequests.Single(r => r.ProfessionalProfileId == 30).Status);
        Assert.Equal(ServiceStatus.Open, service.Status);
    }

    // CA3: a decision is final.
    [Theory]
    [Trait("Requisito", "RF-F008-06")]
    [InlineData(ProposalStatus.Declined)]
    [InlineData(ProposalStatus.Accepted)]
    public void DeclineProposal_PropostaNaoPendente_LancaInvalidTransition(ProposalStatus state)
    {
        var service = CreatePublic();
        var proposal = Submit(service, Profile(12), 101);
        Submit(service, Profile(17), 102);
        if (state == ProposalStatus.Declined)
        {
            service.DeclineProposal(101, ClientId, Decision);
        }
        else
        {
            service.AcceptProposal(102, ClientId, Decision);
        }

        var exception = Assert.Throws<DomainException>(() => service.DeclineProposal(101, ClientId, Decision));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(ProposalStatus.Declined, proposal.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-08")]
    public void DeclineProposal_OutroCliente_LancaForbidden()
    {
        var service = CreatePublic();
        var proposal = Submit(service, Profile(12), 101);

        var exception = Assert.Throws<DomainException>(() => service.DeclineProposal(101, 99, Decision));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
    }

    // ---------- Helpers ----------

    private static User NewClient()
    {
        var client = User.Register("Carla Costa", "carla@localhub.test", UserRole.Client);
        typeof(User).GetProperty(nameof(User.UserId))!.SetValue(client, ClientId);
        return client;
    }

    private static Service CreateDirect(IReadOnlyCollection<ProfessionalProfile> recipients) =>
        Service.Create(NewClient(), Plumbing, ProposalModel.DirectRequest, "Torneira com fuga de água.", Today, null,
            "Rua do Almada 10, Porto", Porto, recipients, Now);

    private static Service CreatePublic() =>
        Service.Create(NewClient(), Plumbing, ProposalModel.Public, "Substituir torneira.", Today, null,
            "Rua do Almada 10, Porto", Porto, [], Now);

    // The ids are normally generated by the database, so they are set by reflection.
    private static Proposal Submit(Service service, ProfessionalProfile provider, int proposalId)
    {
        var proposal = service.SubmitProposal(provider, 80m, "Material incluído.", null, Later);
        typeof(Proposal).GetProperty(nameof(Proposal.ProposalId))!.SetValue(proposal, proposalId);
        return proposal;
    }

    private static ServiceCategory Category(int id, string name)
    {
        var category = ServiceCategory.Create(name);
        typeof(ServiceCategory).GetProperty(nameof(ServiceCategory.ServiceCategoryId))!.SetValue(category, id);
        return category;
    }

    private static ProfessionalProfile Profile(int id)
    {
        var provider = User.Register("Prestador", $"prestador{id}@localhub.test", UserRole.Provider);
        var profile = ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Plumbing], 10m);
        typeof(ProfessionalProfile).GetProperty(nameof(ProfessionalProfile.ProfessionalProfileId))!
            .SetValue(profile, id);
        return profile;
    }
}