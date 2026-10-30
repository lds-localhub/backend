using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

// The history of the changes of state of a service (ServiceStatusHistory): one record per transition of sections 10.1
// and 10.2 of the requirements, with the date and the user that made it (RF-F007-09, RF-F008-01).
[Trait("Category", "Unit")]
public class ServiceStatusHistoryTests
{
    private const int ClientId = 45;
    private const int ProviderUserId = 52;
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);
    private static readonly DateTimeOffset Decision = Now.AddHours(5);
    private static readonly DateTimeOffset End = Now.AddDays(3);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private const decimal PortoLatitude = 41.1579m;
    private const decimal PortoLongitude = -8.6291m;
    private static readonly ServiceCategory Plumbing = Category(3, "Canalização");

    // ---------- Transitions that are recorded ----------

    // Criterion 2: the creation of the service is not a change of state.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RF-F008-01")]
    public void Create_ServicoNovo_NaoTemRegistosNoHistorico()
    {
        var direct = CreateDirect([Profile(12)]);
        var published = CreatePublic();

        Assert.Empty(direct.StatusHistory);
        Assert.Empty(published.StatusHistory);
    }

    // Criterion 1 (E28): OPEN → ASSIGNED, made by the client.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RF-F008-01")]
    public void AcceptProposal_RegistaAPassagemDeAbertoParaAtribuidoPeloCliente()
    {
        var service = CreatePublic();
        Submit(service, Profile(12), 101);

        service.AcceptProposal(101, ClientId, Decision);

        var record = Assert.Single(service.StatusHistory);
        AssertRecord(record, ServiceStatus.Open, ServiceStatus.Assigned, Decision, ClientId);
        Assert.Same(service.Client, record.ChangedBy);
    }

    // Criterion 1 (E30): OPEN → CANCELLED, made by the client.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public void CancelByClient_ServicoAberto_RegistaAPassagemParaCanceladoPeloCliente()
    {
        var service = CreateDirect([Profile(12)]);

        service.CancelByClient(ClientId, Decision);

        var record = Assert.Single(service.StatusHistory);
        AssertRecord(record, ServiceStatus.Open, ServiceStatus.Cancelled, Decision, ClientId);
    }

    // Criterion 1 (E28 and E30): OPEN → ASSIGNED → CANCELLED, both made by the client, in chronological order.
    [Fact]
    [Trait("Requisito", "RF-F008-01")]
    public void CancelByClient_ServicoAtribuido_RegistaAsDuasPassagensPorOrdem()
    {
        var service = AssignedService(out _);

        service.CancelByClient(ClientId, End);

        Assert.Collection(service.StatusHistory,
            first => AssertRecord(first, ServiceStatus.Open, ServiceStatus.Assigned, Later, ClientId),
            second => AssertRecord(second, ServiceStatus.Assigned, ServiceStatus.Cancelled, End, ClientId));
    }

    // Criterion 1 (E30): ASSIGNED → CANCELLED, made by the provider whose proposal was accepted, so that the detail
    // shows who cancelled the service.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RF-F008-01")]
    public void CancelByProvider_RegistaAPassagemParaCanceladoPeloPrestadorContratado()
    {
        var service = AssignedService(out var accepted);

        service.CancelByProvider(12, End);

        var record = service.StatusHistory.Last();
        AssertRecord(record, ServiceStatus.Assigned, ServiceStatus.Cancelled, End, ProviderUserId);
        Assert.Same(accepted.ProfessionalProfile.ServiceProvider, record.ChangedBy);
        Assert.Equal(UserRole.Provider, record.ChangedBy.Role);
    }

    // Criterion 1 (E31): ASSIGNED → COMPLETED, made by the client.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RF-F008-01")]
    public void Complete_RegistaAPassagemDeAtribuidoParaRealizadoPeloCliente()
    {
        var service = AssignedService(out _);

        service.Complete(ClientId, End);

        Assert.Equal(2, service.StatusHistory.Count);
        AssertRecord(service.StatusHistory.Last(), ServiceStatus.Assigned, ServiceStatus.Completed, End, ClientId);
    }

    // ---------- Operations that do not change the state ----------

    // Criterion 3 (E29): declining a proposal keeps the service OPEN.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public void DeclineProposal_NaoGeraRegistoNoHistorico()
    {
        var service = CreatePublic();
        Submit(service, Profile(12), 101);

        service.DeclineProposal(101, ClientId, Decision);

        Assert.Empty(service.StatusHistory);
    }

    // E24: declining a direct request keeps the service OPEN.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public void DeclineDirectRequest_NaoGeraRegistoNoHistorico()
    {
        var service = CreateDirect([Profile(12)]);
        var request = service.DirectRequests.Single();
        typeof(DirectRequest).GetProperty(nameof(DirectRequest.DirectRequestId))!.SetValue(request, 510);

        service.DeclineDirectRequest(510, 12, Decision);

        Assert.Empty(service.StatusHistory);
    }

    // ---------- Refused transitions ----------

    // Criterion 4: a refused transition leaves the history as it was.
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public void AcceptProposal_OutroCliente_NaoGeraRegisto()
    {
        var service = CreatePublic();
        Submit(service, Profile(12), 101);

        Assert.Throws<DomainException>(() => service.AcceptProposal(101, 99, Decision));

        Assert.Empty(service.StatusHistory);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-01")]
    public void CancelByProvider_PropostaNaoAceite_NaoGeraRegisto()
    {
        var service = AssignedService(out _);

        Assert.Throws<DomainException>(() => service.CancelByProvider(17, End));

        Assert.Single(service.StatusHistory);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public void Complete_ServicoAberto_NaoGeraRegisto()
    {
        var service = CreatePublic();

        Assert.Throws<DomainException>(() => service.Complete(ClientId, End));

        Assert.Empty(service.StatusHistory);
    }

    // COMPLETED and CANCELLED are final: a second change is refused and not recorded.
    [Fact]
    [Trait("Requisito", "RF-F008-01")]
    public void CancelByClient_ServicoRealizado_NaoGeraRegisto()
    {
        var service = AssignedService(out _);
        service.Complete(ClientId, End);

        Assert.Throws<DomainException>(() => service.CancelByClient(ClientId, End.AddDays(1)));

        Assert.Equal(2, service.StatusHistory.Count);
        Assert.Equal(ServiceStatus.Completed, service.StatusHistory.Last().NewStatus);
    }

    // ---------- Helpers ----------

    private static void AssertRecord(
        ServiceStatusHistory record,
        ServiceStatus previousStatus,
        ServiceStatus newStatus,
        DateTimeOffset changedAt,
        int changedByUserId)
    {
        Assert.Equal(previousStatus, record.PreviousStatus);
        Assert.Equal(newStatus, record.NewStatus);
        Assert.Equal(changedAt, record.ChangedAt);
        Assert.Equal(changedByUserId, record.ChangedByUserId);
        Assert.Equal(changedByUserId, record.ChangedBy.UserId);
    }

    // A published job with the proposal of the profile 12 (provider 52) accepted and the proposal of the profile 17
    // declined.
    private static Service AssignedService(out Proposal accepted)
    {
        var service = CreatePublic();
        accepted = Submit(service, Profile(12), 101);
        Submit(service, Profile(17), 102);
        service.AcceptProposal(101, ClientId, Later);
        return service;
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

    // The provider of the profile 12 has the user id 52; the others keep the id 0.
    private static ProfessionalProfile Profile(int id)
    {
        var provider = User.Register("Prestador", $"prestador{id}@localhub.test", UserRole.Provider);
        if (id == 12)
        {
            typeof(User).GetProperty(nameof(User.UserId))!.SetValue(provider, ProviderUserId);
        }

        var profile = ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Plumbing], 10);
        typeof(ProfessionalProfile).GetProperty(nameof(ProfessionalProfile.ProfessionalProfileId))!
            .SetValue(profile, id);
        return profile;
    }
}
