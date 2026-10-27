using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// Represents a service requested by a client, with direct requests to chosen providers (F009) or published as a job
/// without recipients (F010). Both modalities share the same life cycle (Requirements Specification v04, section 10).
/// </summary>
public class Service
{
    // Limits of the API documentation (E20), the same as the columns of the data model. The DTOs use the same
    // constants (rule R17).
    public const int TitleMaxLength = 100;
    public const int DescriptionMaxLength = 1000;
    public const int LocationMaxLength = 255;

    public int ServiceId { get; private set; }
    public int ClientId { get; private set; }
    public int ServiceCategoryId { get; private set; }
    public ProposalModel ProposalModel { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public DateOnly RequestedStartDate { get; private set; }
    public DateOnly? RequestedEndDate { get; private set; }
    public ServiceStatus Status { get; private set; }
    public string Location { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }
    public DateTimeOffset? ActualCompletionDate { get; private set; }

    // Navigations (they are not columns). "= null!": always set by Create or loaded by Entity Framework with Include.
    public User Client { get; private set; } = null!;
    public ServiceCategory ServiceCategory { get; private set; } = null!;

    private readonly List<Proposal> _proposals = [];
    public IReadOnlyCollection<Proposal> Proposals => _proposals;

    private readonly List<DirectRequest> _directRequests = [];
    public IReadOnlyCollection<DirectRequest> DirectRequests => _directRequests;

    // Null while the service was not reviewed (at most one review per service).
    public Review? Review { get; private set; }

    private readonly List<ServiceStatusHistory> _statusHistory = [];
    public IReadOnlyCollection<ServiceStatusHistory> StatusHistory => _statusHistory;

    // Private: a service is created with Create. Entity Framework uses this constructor when it reads a service.
    private Service() { }

    // RF-F009-01 and RF-F010-01 (E20). The fields were validated by the DTO, and the category, the dates, the
    // recipients and the location by the ServiceWorkflowService (rule R17). The location is already resolved (the
    // client's registered location or the address converted into coordinates), because the geocoding happens before
    // any entity is changed (rule R2). The recipients are the profiles chosen by the client: at least one with
    // DIRECT_REQUEST and none with PUBLIC. The service starts OPEN and is only assigned when the client accepts a
    // proposal (no automatic assignment).
    public static Service Create(
        User client,
        ServiceCategory category,
        ProposalModel proposalModel,
        string title,
        string description,
        DateOnly requestedStartDate,
        DateOnly? requestedEndDate,
        string location,
        decimal latitude,
        decimal longitude,
        IReadOnlyCollection<ProfessionalProfile> recipients,
        DateTimeOffset now)
    {
        if (client.Role != UserRole.Client)
        {
            throw new DomainException(DomainErrorKind.Forbidden, "Só um Cliente pode criar serviços.");
        }

        var service = new Service
        {
            Client = client,
            ClientId = client.UserId,
            ServiceCategory = category,
            ServiceCategoryId = category.ServiceCategoryId,
            ProposalModel = proposalModel,
            Title = title.Trim(),
            Description = description.Trim(),
            RequestedStartDate = requestedStartDate,
            RequestedEndDate = requestedEndDate,
            Status = ServiceStatus.Open,
            Location = location.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            CreationDate = now,
        };

        // The same profile chosen twice gets a single direct request (RF-F009-01, rule 9).
        var distinct = recipients
            .GroupBy(p => p.ProfessionalProfileId)
            .Select(g => g.First());
        foreach (var profile in distinct)
        {
            service._directRequests.Add(new DirectRequest(profile, now));
        }

        return service;
    }

    // RF-F007-01 and RF-F011-02 (E25), sections 10.3 and 10.4. The provider must have the category of the service and,
    // with DIRECT_REQUEST, a direct request. The Client (loaded with the service) must have an ACTIVE account.
    // The provider's areas must be loaded, and the direct requests and the proposals of the provider too.
    // A provider presents one proposal per service, whatever the state of the first one.
    public Proposal SubmitProposal(
        ProfessionalProfile provider, decimal amount, string conditions, string? obs, DateTimeOffset now)
    {
        if (Client.Status != ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Não pode apresentar propostas a um serviço de um Cliente com a conta desativada.");
        }

        if (provider.ServiceAreas.All(a => a.ServiceCategoryId != ServiceCategoryId))
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "O seu perfil não tem a categoria deste serviço.");
        }

        var request = _directRequests.SingleOrDefault(r => r.ProfessionalProfileId == provider.ProfessionalProfileId);
        if (ProposalModel == ProposalModel.DirectRequest && request is null)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Não recebeu uma solicitação direta para este serviço.");
        }

        if (Status != ServiceStatus.Open)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "O serviço já não está aberto.");
        }

        if (_proposals.Any(p => p.ProfessionalProfileId == provider.ProfessionalProfileId))
        {
            throw new DomainException(DomainErrorKind.Conflict, "Já apresentou uma proposta a este serviço.");
        }

        var proposal = new Proposal(provider, amount, conditions, obs, now);
        request?.MarkProposalSubmitted(now);
        _proposals.Add(proposal);
        return proposal;
    }

    // RF-F007-02 (E24), section 10.4: only the provider that received the request declines it, while the service is
    // OPEN. The service stays OPEN, even without other pending requests and proposals.
    public DirectRequest DeclineDirectRequest(int directRequestId, int professionalProfileId, DateTimeOffset now)
    {
        var request = _directRequests.SingleOrDefault(r => r.DirectRequestId == directRequestId)
                      ?? throw new DomainException(DomainErrorKind.Conflict,
                          "A solicitação direta não pertence a este serviço.");
        if (request.ProfessionalProfileId != professionalProfileId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Prestador destinatário pode recusar a solicitação direta.");
        }

        if (Status != ServiceStatus.Open)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "O serviço já não está aberto.");
        }

        request.Decline(now);
        return request;
    }
}
