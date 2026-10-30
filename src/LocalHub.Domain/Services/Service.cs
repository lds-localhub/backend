using System.Linq.Expressions;

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

    // RF-F006-01 and RF-F011-01 (E19, E38): the published jobs that any provider with a profile can find and consult —
    // PUBLIC, OPEN and of a Client with an ACTIVE account. It is an expression, so that Entity Framework translates it
    // into the WHERE of the queries; in C# (Compile()) the Client must be loaded.
    public static readonly Expression<Func<Service, bool>> IsOpenPublishedJob =
        s => s.ProposalModel == ProposalModel.Public
             && s.Status == ServiceStatus.Open
             && s.Client.Status == ActivationStatus.Active;

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

    private readonly List<Proposal> _proposals = new();
    public IReadOnlyCollection<Proposal> Proposals => _proposals;

    private readonly List<DirectRequest> _directRequests = new();
    public IReadOnlyCollection<DirectRequest> DirectRequests => _directRequests;

    // Null while the service was not reviewed (at most one review per service).
    public Review? Review { get; private set; }

    private readonly List<ServiceStatusHistory> _statusHistory = new();
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
        var request = _directRequests.SingleOrDefault(r => r.DirectRequestId == directRequestId);
        if (request is null)
        {
            throw new DomainException(DomainErrorKind.Conflict,
                "A solicitação direta não pertence a este serviço.");
        }

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

    // RF-F007-03 and RF-F008-02 (E28), sections 10.1 to 10.4: only the client that created the service accepts a
    // PENDING proposal of an OPEN service. The service becomes ASSIGNED, the other PENDING proposals DECLINED and the
    // direct requests still PENDING become VOID. The provider of the proposal must have an ACTIVE account
    // (RF-F002-02, rule 7). The proposals must be loaded with the profile and its provider, and all the direct requests.
    // PENDING: the change of state of the service is not recorded in ServiceStatusHistory yet (I050).
    public Proposal AcceptProposal(int proposalId, int clientId, DateTimeOffset now)
    {
        var proposal = FindProposal(proposalId, clientId);
        if (Status != ServiceStatus.Open)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "O serviço já não está aberto.");
        }

        if (proposal.Status != ProposalStatus.Pending)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "A proposta já não está pendente.");
        }

        if (proposal.ProfessionalProfile.ServiceProvider.Status != ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Conflict,
                "Não é possível aceitar a proposta de um Prestador com a conta desativada.");
        }

        Status = ServiceStatus.Assigned;
        proposal.Accept(now);
        foreach (var other in _proposals.Where(p => p != proposal && p.Status == ProposalStatus.Pending))
        {
            other.Decline(now);
        }

        foreach (var request in _directRequests.Where(r => r.Status == DirectRequestStatus.Pending))
        {
            request.Void();
        }

        return proposal;
    }

    // RF-F007-04 and RF-F008-03 (E29), section 10.3: only the client that created the service declines a PENDING
    // proposal of an OPEN service. The other proposals, the direct requests and the service do not change.
    public Proposal DeclineProposal(int proposalId, int clientId, DateTimeOffset now)
    {
        var proposal = FindProposal(proposalId, clientId);
        if (Status != ServiceStatus.Open)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "O serviço já não está aberto.");
        }

        proposal.Decline(now);
        return proposal;
    }

    // RF-F007-05 and RF-F008-04 (E30), sections 10.1 to 10.4: the client that created the service cancels it while it is
    // OPEN or ASSIGNED. In OPEN, the PENDING proposals and the PENDING direct requests become VOID; in ASSIGNED, the
    // ACCEPTED proposal stays ACCEPTED. The DECLINED proposals and the answered direct requests do not change.
    // The proposals and the direct requests must be loaded.
    // PENDING: the change of state of the service is not recorded in ServiceStatusHistory yet (I050).
    public void CancelByClient(int clientId, DateTimeOffset now)
    {
        if (clientId != ClientId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Cliente que criou o serviço ou o Prestador contratado podem cancelá-lo.");
        }

        EnsureCanBeCancelled();
        if (Status == ServiceStatus.Open)
        {
            foreach (var proposal in _proposals.Where(p => p.Status == ProposalStatus.Pending))
            {
                proposal.Void(now);
            }

            foreach (var request in _directRequests.Where(r => r.Status == DirectRequestStatus.Pending))
            {
                request.Void();
            }
        }

        Status = ServiceStatus.Cancelled;
    }

    // RF-F007-05 and RF-F008-04 (E30), sections 10.1 and 10.2: the provider whose proposal was accepted cancels the
    // service, only while it is ASSIGNED; the ACCEPTED proposal stays ACCEPTED. A provider that only received a direct
    // request or whose proposal was not accepted cannot cancel (403). The proposals must be loaded.
    // PENDING: the change of state of the service is not recorded in ServiceStatusHistory yet (I050).
    public void CancelByProvider(int professionalProfileId)
    {
        var accepted = _proposals.SingleOrDefault(p => p.Status == ProposalStatus.Accepted);
        if (accepted is null || accepted.ProfessionalProfileId != professionalProfileId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Prestador com a proposta aceite pode cancelar o serviço, e apenas no estado atribuído.");
        }

        EnsureCanBeCancelled();
        Status = ServiceStatus.Cancelled;
    }

    // RF-F007-06 and RF-F008-05 (E31), sections 10.1 and 10.2: only the client that created the service marks it as
    // completed, only while it is ASSIGNED. The date of completion is recorded and the service can then be reviewed
    // (RF-F012-01). The ACCEPTED proposal stays ACCEPTED.
    // PENDING: the change of state of the service is not recorded in ServiceStatusHistory yet (I050).
    public void Complete(int clientId, DateTimeOffset now)
    {
        if (clientId != ClientId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Cliente que criou o serviço pode marcá-lo como realizado.");
        }

        if (Status != ServiceStatus.Assigned)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition,
                "Só é possível marcar como realizado um serviço no estado atribuído.");
        }

        Status = ServiceStatus.Completed;
        ActualCompletionDate = now;
    }

    // RF-F012-01 (E32): only the client that created the service reviews it, only while it is COMPLETED and only once.
    // The review is of the provider whose proposal was accepted (rule 6), so a COMPLETED service always has one
    // (sections 10.1 and 10.2). The service stays COMPLETED. The review must be loaded.
    public Review AddReview(int clientId, short rating, string? comment, DateTimeOffset now)
    {
        if (clientId != ClientId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Cliente que criou o serviço pode avaliá-lo.");
        }

        if (Status != ServiceStatus.Completed)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition,
                "Só é possível avaliar um serviço no estado realizado.");
        }

        if (Review is not null)
        {
            throw new DomainException(DomainErrorKind.Conflict, "Este serviço já foi avaliado.",
                errorType: Review.AlreadyExistsErrorType);
        }

        Review = new Review(this, rating, comment, now);
        return Review;
    }

    // COMPLETED and CANCELLED are final states (RF-F007-07, RF-F008-06).
    private void EnsureCanBeCancelled()
    {
        if (Status == ServiceStatus.Completed)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition,
                "Não é possível cancelar um serviço no estado realizado.");
        }

        if (Status == ServiceStatus.Cancelled)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "O serviço já está cancelado.");
        }
    }

    // The client that created the service is the only actor of the decisions on the proposals (section 10.3).
    private Proposal FindProposal(int proposalId, int clientId)
    {
        if (clientId != ClientId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Cliente que criou o serviço pode decidir sobre as propostas.");
        }

        var proposal = _proposals.SingleOrDefault(p => p.ProposalId == proposalId);
        if (proposal is null)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A proposta não pertence a este serviço.");
        }

        return proposal;
    }
}