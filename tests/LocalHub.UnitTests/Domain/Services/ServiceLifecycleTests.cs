using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

// E30 and E31: the cancellation and the completion of a service (sections 10.1 to 10.4 of the requirements).
[Trait("Category", "Unit")]
public class ServiceLifecycleTests
{
    private const int ClientId = 45;
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);
    private static readonly DateTimeOffset Decision = Now.AddHours(5);
    private static readonly DateTimeOffset End = Now.AddDays(3);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private const decimal PortoLatitude = 41.1579m;
    private const decimal PortoLongitude = -8.6291m;
    private static readonly ServiceCategory Plumbing = Category(3, "Canalização");

    // ---------- CancelByClient (RF-F007-05, RF-F008-04) ----------

    // Criteria 1 and 2 of RF-F007-05: OPEN → CANCELLED; PENDING → VOID; DECLINED and answered requests stay.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public void CancelByClient_ServicoAberto_CancelaEAnulaAsPropostasESolicitacoesPendentes()
    {
        var silent = Profile(30);
        var service = CreateDirect([Profile(12), Profile(17), Profile(21), silent]);
        var pending = Submit(service, service.DirectRequests.ElementAt(0).ProfessionalProfile, 101);
        var declined = Submit(service, service.DirectRequests.ElementAt(1).ProfessionalProfile, 102);
        service.DeclineProposal(102, ClientId, Later);
        var declinedRequest = service.DirectRequests.Single(r => r.ProfessionalProfileId == 21);
        typeof(DirectRequest).GetProperty(nameof(DirectRequest.DirectRequestId))!.SetValue(declinedRequest, 510);
        service.DeclineDirectRequest(510, 21, Later);

        service.CancelByClient(ClientId, Decision);

        Assert.Equal(ServiceStatus.Cancelled, service.Status);
        Assert.Equal(ProposalStatus.Void, pending.Status);
        Assert.Equal(Decision, pending.DecisionDate);
        Assert.Equal(ProposalStatus.Declined, declined.Status);
        Assert.Equal(Later, declined.DecisionDate);
        var requests = service.DirectRequests.ToDictionary(r => r.ProfessionalProfileId, r => r.Status);
        Assert.Equal(DirectRequestStatus.Void, requests[30]);
        Assert.Equal(DirectRequestStatus.Declined, requests[21]);
        Assert.Equal(DirectRequestStatus.ProposalSubmitted, requests[12]);
        Assert.Equal(DirectRequestStatus.ProposalSubmitted, requests[17]);
        Assert.Null(service.ActualCompletionDate);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-04")]
    public void CancelByClient_TrabalhoAberto_CancelaEAnulaAsPropostasPendentes()
    {
        var service = CreatePublic();
        var first = Submit(service, Profile(12), 101);
        var second = Submit(service, Profile(17), 102);

        service.CancelByClient(ClientId, Decision);

        Assert.Equal(ServiceStatus.Cancelled, service.Status);
        Assert.All([first, second], p =>
        {
            Assert.Equal(ProposalStatus.Void, p.Status);
            Assert.Equal(Decision, p.DecisionDate);
        });
    }

    // Criterion 3: ASSIGNED → CANCELLED; the ACCEPTED proposal stays ACCEPTED and the DECLINED ones stay DECLINED.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public void CancelByClient_ServicoAtribuido_CancelaEMantemAPropostaAceite()
    {
        var service = CreatePublic();
        var accepted = Submit(service, Profile(12), 101);
        var other = Submit(service, Profile(17), 102);
        service.AcceptProposal(101, ClientId, Later);

        service.CancelByClient(ClientId, Decision);

        Assert.Equal(ServiceStatus.Cancelled, service.Status);
        Assert.Equal(ProposalStatus.Accepted, accepted.Status);
        Assert.Equal(Later, accepted.DecisionDate);
        Assert.Equal(ProposalStatus.Declined, other.Status);
        Assert.Equal(Later, other.DecisionDate);
    }

    // Criterion 6: only the client that created the service.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public void CancelByClient_OutroCliente_LancaForbiddenSemAlterarNada()
    {
        var service = CreatePublic();
        var proposal = Submit(service, Profile(12), 101);

        var exception = Assert.Throws<DomainException>(() => service.CancelByClient(99, Decision));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
    }

    // Criterion 7 and section 10: COMPLETED and CANCELLED are final.
    [Theory]
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    [InlineData(ServiceStatus.Completed)]
    [InlineData(ServiceStatus.Cancelled)]
    public void CancelByClient_EstadoFinal_LancaInvalidTransitionEOEstadoMantemSe(ServiceStatus state)
    {
        var service = AssignedService(out _);
        MoveTo(service, state);

        var exception = Assert.Throws<DomainException>(() => service.CancelByClient(ClientId, Decision));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(state, service.Status);
    }

    // ---------- CancelByProvider (RF-F007-05, RF-F008-04) ----------

    // Criterion 4: the provider whose proposal was accepted cancels an ASSIGNED service.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public void CancelByProvider_PrestadorContratado_CancelaEMantemAPropostaAceite()
    {
        var service = AssignedService(out var accepted);

        service.CancelByProvider(12, Decision);

        Assert.Equal(ServiceStatus.Cancelled, service.Status);
        Assert.Equal(ProposalStatus.Accepted, accepted.Status);
    }

    // Rule 2: a provider that only received a direct request, in an OPEN service.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public void CancelByProvider_SoRecebeuASolicitacao_LancaForbiddenSemAlterarNada()
    {
        var service = CreateDirect([Profile(12)]);

        var exception = Assert.Throws<DomainException>(() => service.CancelByProvider(12, Decision));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(DirectRequestStatus.Pending, service.DirectRequests.Single().Status);
    }

    // A provider with a PENDING proposal cannot cancel an OPEN service either.
    [Fact]
    [Trait("Requisito", "RF-F008-04")]
    public void CancelByProvider_ServicoAbertoComPropostaPendente_LancaForbidden()
    {
        var service = CreatePublic();
        var proposal = Submit(service, Profile(12), 101);

        var exception = Assert.Throws<DomainException>(() => service.CancelByProvider(12, Decision));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ServiceStatus.Open, service.Status);
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
    }

    // Criterion 5: a provider whose proposal was not accepted.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public void CancelByProvider_PropostaNaoAceite_LancaForbidden()
    {
        var service = AssignedService(out _);

        var exception = Assert.Throws<DomainException>(() => service.CancelByProvider(17, Decision));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ServiceStatus.Assigned, service.Status);
    }

    [Theory]
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    [InlineData(ServiceStatus.Completed)]
    [InlineData(ServiceStatus.Cancelled)]
    public void CancelByProvider_EstadoFinal_LancaInvalidTransition(ServiceStatus state)
    {
        var service = AssignedService(out _);
        MoveTo(service, state);

        var exception = Assert.Throws<DomainException>(() => service.CancelByProvider(12, Decision));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(state, service.Status);
    }

    // ---------- Complete (RF-F007-06, RF-F008-05) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-06")]
    [Trait("Requisito", "RF-F008-05")]
    public void Complete_ServicoAtribuido_FicaRealizadoComADataDeRealizacao()
    {
        var service = AssignedService(out var accepted);

        service.Complete(ClientId, End);

        Assert.Equal(ServiceStatus.Completed, service.Status);
        Assert.Equal(End, service.ActualCompletionDate);
        Assert.Equal(ProposalStatus.Accepted, accepted.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-06")]
    public void Complete_OutroCliente_LancaForbidden()
    {
        var service = AssignedService(out _);

        var exception = Assert.Throws<DomainException>(() => service.Complete(99, End));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Equal(ServiceStatus.Assigned, service.Status);
        Assert.Null(service.ActualCompletionDate);
    }

    // Section 10: OPEN → COMPLETED is not a transition; COMPLETED and CANCELLED are final.
    [Theory]
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    [InlineData(ServiceStatus.Open)]
    [InlineData(ServiceStatus.Completed)]
    [InlineData(ServiceStatus.Cancelled)]
    public void Complete_ServicoNaoAtribuido_LancaInvalidTransition(ServiceStatus state)
    {
        Service service;
        if (state == ServiceStatus.Open)
        {
            service = CreatePublic();
        }
        else
        {
            service = AssignedService(out _);
            MoveTo(service, state);
        }

        var completionDate = service.ActualCompletionDate;

        var exception = Assert.Throws<DomainException>(() => service.Complete(ClientId, End.AddDays(1)));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Equal(state, service.Status);
        Assert.Equal(completionDate, service.ActualCompletionDate);
    }

    // ---------- Helpers ----------

    // A published job with the proposal of the profile 12 accepted and the proposal of the profile 17 declined.
    private static Service AssignedService(out Proposal accepted)
    {
        var service = CreatePublic();
        accepted = Submit(service, Profile(12), 101);
        Submit(service, Profile(17), 102);
        service.AcceptProposal(101, ClientId, Later);
        return service;
    }

    // From ASSIGNED to a final state.
    private static void MoveTo(Service service, ServiceStatus state)
    {
        if (state == ServiceStatus.Completed)
        {
            service.Complete(ClientId, End);
        }
        else
        {
            service.CancelByClient(ClientId, End);
        }
    }

    private static User NewClient()
    {
        var client = User.Register("Carla Costa", "carla@localhub.test", UserRole.Client);
        typeof(User).GetProperty(nameof(User.UserId))!.SetValue(client, ClientId);
        return client;
    }

    private static Service CreateDirect(IReadOnlyCollection<ProfessionalProfile> recipients) =>
        Service.Create(NewClient(), Plumbing, ProposalModel.DirectRequest, "Reparar torneira",
            "Torneira com fuga de água.", Today, null, "Rua do Almada 10, Porto", PortoLatitude, PortoLongitude,
            recipients, Now);

    private static Service CreatePublic() =>
        Service.Create(NewClient(), Plumbing, ProposalModel.Public, "Reparar torneira", "Substituir torneira.", Today,
            null, "Rua do Almada 10, Porto", PortoLatitude, PortoLongitude, [], Now);

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
        var profile = ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Plumbing], 10);
        typeof(ProfessionalProfile).GetProperty(nameof(ProfessionalProfile.ProfessionalProfileId))!
            .SetValue(profile, id);
        return profile;
    }
}