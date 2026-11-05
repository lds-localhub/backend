using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// A service requested by a client, with direct requests to chosen providers or public. Both share the same life cycle.
/// </summary>
public class Service
{
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

    // Navigations: set by Create or loaded by Entity Framework with Include.
    public User Client { get; private set; } = null!;
    public ServiceCategory ServiceCategory { get; private set; } = null!;

    private readonly List<Proposal> _proposals = new();
    public IReadOnlyCollection<Proposal> Proposals => _proposals;

    private readonly List<DirectRequest> _directRequests = new();
    public IReadOnlyCollection<DirectRequest> DirectRequests => _directRequests;

    public Review? Review { get; private set; }

    private readonly List<ServiceStatusHistory> _statusHistory = new();
    public IReadOnlyCollection<ServiceStatusHistory> StatusHistory => _statusHistory;

    // Used by Entity Framework; a service is created with Create.
    private Service() { }

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

        // The same profile chosen twice gets a single direct request.
        var distinct = recipients
            .GroupBy(p => p.ProfessionalProfileId)
            .Select(g => g.First());
        foreach (var profile in distinct)
        {
            service._directRequests.Add(new DirectRequest(profile, now));
        }

        return service;
    }

    // Needs the client, the provider's areas and the provider's direct requests and proposals loaded.
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
        if (ProposalModel == ProposalModel.DirectRequest && request == null)
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

    public DirectRequest DeclineDirectRequest(int directRequestId, int professionalProfileId, DateTimeOffset now)
    {
        var request = _directRequests.SingleOrDefault(r => r.DirectRequestId == directRequestId);
        if (request == null)
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

    // Needs the proposals (with profile and provider), the direct requests and the client loaded.
    // The other pending proposals are declined and the pending direct requests become void.
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
                "Não é possível aceitar a proposta de um Prestador com a conta desativada.",
                errorType: Proposal.InactiveProviderErrorType);
        }

        ChangeStatus(ServiceStatus.Assigned, Client, now);
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

    // Needs the proposals, the direct requests and the client loaded.
    // In OPEN the pending proposals and direct requests become void; in ASSIGNED the accepted proposal stays.
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
                proposal.Void();
            }

            foreach (var request in _directRequests.Where(r => r.Status == DirectRequestStatus.Pending))
            {
                request.Void();
            }
        }

        ChangeStatus(ServiceStatus.Cancelled, Client, now);
    }

    public void CancelByProvider(int professionalProfileId, DateTimeOffset now)
    {
        var accepted = _proposals.SingleOrDefault(p => p.Status == ProposalStatus.Accepted);
        if (accepted == null || accepted.ProfessionalProfileId != professionalProfileId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Prestador com a proposta aceite pode cancelar o serviço, e apenas no estado atribuído.");
        }

        EnsureCanBeCancelled();
        ChangeStatus(ServiceStatus.Cancelled, accepted.ProfessionalProfile.ServiceProvider, now);
    }

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

        ChangeStatus(ServiceStatus.Completed, Client, now);
        ActualCompletionDate = now;
    }

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

        if (Review != null)
        {
            throw new DomainException(DomainErrorKind.Conflict, "Este serviço já foi avaliado.",
                errorType: Review.AlreadyExistsErrorType);
        }

        Review = new Review(this, rating, comment, now);
        return Review;
    }

    // The creation of the service is not recorded as a change of state.
    private void ChangeStatus(ServiceStatus newStatus, User changedBy, DateTimeOffset now)
    {
        _statusHistory.Add(new ServiceStatusHistory(Status, newStatus, changedBy, now));
        Status = newStatus;
    }

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

    private Proposal FindProposal(int proposalId, int clientId)
    {
        if (clientId != ClientId)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só o Cliente que criou o serviço pode decidir sobre as propostas.");
        }

        var proposal = _proposals.SingleOrDefault(p => p.ProposalId == proposalId);
        if (proposal == null)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A proposta não pertence a este serviço.");
        }

        return proposal;
    }
}