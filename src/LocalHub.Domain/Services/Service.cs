using System.Linq.Expressions;

using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// Represents a service requested by a client, with direct requests to chosen providers (F009) or published as a job
/// without recipients (F010). Both modalities share the same life cycle (Requirements Specification v03, section 10).
/// </summary>
public class Service
{
    // Limits of the API documentation (E20), the same as the columns of the data model.
    public const int DescriptionMaxLength = 1000;
    public const int LocationMaxLength = 255;

    public const string ProposalModelField = "proposalModel";
    public const string CategoryField = "serviceCategoryId";
    public const string DescriptionField = "description";
    public const string StartDateField = "requestedStartDate";
    public const string EndDateField = "requestedEndDate";
    public const string LocationField = "location";
    public const string RecipientsField = "professionalProfileIds";

    // The requested dates are compared with the date in Portugal, like the CHECK constraint of the data model.
    private static readonly TimeZoneInfo Portugal = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    // RF-F006-01 and RF-F011-01 (E19, E38): the published jobs that any provider with a profile can find and consult —
    // PUBLIC, OPEN and of a Client with an ACTIVE account. It is an expression, so that Entity Framework translates it
    // into the WHERE of the queries; in C# (Compile()) the Client must be loaded.
    public static readonly Expression<Func<Service, bool>> IsOpenPublishedJob =
        s => s.ProposalModel == ProposalModel.Public
             && s.Status == ServiceStatus.Open
             && s.Client.Status == ActivationStatus.Active;

    public int ServiceId { get; private set; }
    public int ClientId { get; private set; }
    // "= null!": always set by Create or loaded by Entity Framework with Include.
    public User Client { get; private set; } = null!;
    public int ServiceCategoryId { get; private set; }
    public ServiceCategory ServiceCategory { get; private set; } = null!;
    public ProposalModel ProposalModel { get; private set; }
    public string Description { get; private set; } = null!;
    public DateOnly RequestedStartDate { get; private set; }
    public DateOnly? RequestedEndDate { get; private set; }
    public ServiceStatus Status { get; private set; }
    public string Location { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }
    public DateTimeOffset? ActualCompletionDate { get; private set; }

    private readonly List<DirectRequest> _directRequests = [];
    public IReadOnlyCollection<DirectRequest> DirectRequests => _directRequests;

    private readonly List<Proposal> _proposals = [];
    public IReadOnlyCollection<Proposal> Proposals => _proposals;

    // Private: a service is created with Create. Entity Framework uses this constructor when it reads a service.
    private Service() { }

    // RF-F009-01 and RF-F010-01 (E20). The location is already resolved (the client's registered location or the
    // address converted into coordinates), because the geocoding happens before any entity is changed (rule R2).
    // The recipients are the profiles chosen by the client: at least one with DIRECT_REQUEST and none with PUBLIC.
    // The service starts OPEN and is only assigned when the client accepts a proposal (no automatic assignment).
    public static Service Create(
        User client,
        ServiceCategory category,
        ProposalModel proposalModel,
        string? description,
        DateOnly? requestedStartDate,
        DateOnly? requestedEndDate,
        string? location,
        GeoPoint coordinates,
        IReadOnlyCollection<ProfessionalProfile> recipients,
        DateTimeOffset now)
    {
        if (client.Role != UserRole.Client)
        {
            throw new DomainException(DomainErrorKind.Forbidden, "Só um Cliente pode criar serviços.");
        }

        ValidateProposalModel(proposalModel);
        ValidateCategory(category);
        var (startDate, endDate) = ValidateRequestedDates(requestedStartDate, requestedEndDate, TodayInPortugal(now));

        var service = new Service
        {
            Client = client,
            ClientId = client.UserId,
            ServiceCategory = category,
            ServiceCategoryId = category.ServiceCategoryId,
            ProposalModel = proposalModel,
            Description = ValidateDescription(description),
            RequestedStartDate = startDate,
            RequestedEndDate = endDate,
            Status = ServiceStatus.Open,
            Location = ValidateLocation(location),
            Latitude = coordinates.Latitude,
            Longitude = coordinates.Longitude,
            CreationDate = now,
        };

        // The same profile chosen twice gets a single direct request (RF-F009-01, rule 9).
        var distinct = recipients
            .GroupBy(p => p.ProfessionalProfileId)
            .Select(g => g.First())
            .ToList();
        ValidateRecipientCount(proposalModel, distinct.Count);

        var ineligible = DescribeIneligible(distinct, category.ServiceCategoryId);
        if (ineligible.Count > 0)
        {
            throw new DomainException(DomainErrorKind.Validation, string.Join(" ", ineligible), RecipientsField);
        }

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
        ProfessionalProfile provider, decimal? amount, string? conditions, string? obs, DateTimeOffset now)
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

    // The date of "now" in Portugal: a single date equal to it is accepted (RF-F010-01, rule 5).
    public static DateOnly TodayInPortugal(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Portugal).DateTime);

    // The validations below are public so that the service of the application can collect the errors of every
    // field in a single 400 response.

    public static void ValidateProposalModel(ProposalModel? proposalModel)
    {
        // Enum.IsDefined: the JSON reader also accepts numbers, such as 7, that are not a modality.
        if (proposalModel is null || !Enum.IsDefined(proposalModel.Value))
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Indique a modalidade: DIRECT_REQUEST ou PUBLIC.", ProposalModelField);
        }
    }

    // RF-F003-03, rule 3: a deactivated category cannot be used in new services.
    public static void ValidateCategory(ServiceCategory category)
    {
        if (category.Status != ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"A categoria «{category.Name}» está desativada. Escolha uma categoria ativa.", CategoryField);
        }
    }

    // RF-F009-01, rule 2; RF-F010-01, rule 4. Same rule as the CHECK constraint: not empty and not only spaces.
    public static string ValidateDescription(string? description)
    {
        string trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > DescriptionMaxLength)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"A descrição é obrigatória e tem no máximo {DescriptionMaxLength} caracteres.", DescriptionField);
        }

        return trimmed;
    }

    // RF-F010-01, rule 5: a single date (start only) or an interval. The start cannot be before today and the end
    // cannot be before the start.
    public static (DateOnly Start, DateOnly? End) ValidateRequestedDates(DateOnly? start, DateOnly? end, DateOnly today)
    {
        if (start is null)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Indique a data pretendida para o serviço.", StartDateField);
        }

        if (start.Value < today)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "A data pretendida tem de ser igual ou posterior à data de hoje.", StartDateField);
        }

        if (end is not null && end.Value < start.Value)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "A data de fim tem de ser igual ou posterior à data de início.", EndDateField);
        }

        return (start.Value, end);
    }

    // The location where the service is done: the client's registered address or the address that was indicated.
    public static string ValidateLocation(string? location)
    {
        string trimmed = location?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Indique o local de realização do serviço.", LocationField);
        }

        if (trimmed.Length > LocationMaxLength)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"A morada tem no máximo {LocationMaxLength} caracteres.", LocationField);
        }

        return trimmed;
    }

    // DIRECT_REQUEST needs at least one provider (RF-F009-01, rule 4); PUBLIC has no recipients (RF-F010-01).
    public static void ValidateRecipientCount(ProposalModel proposalModel, int count)
    {
        if (proposalModel == ProposalModel.DirectRequest && count == 0)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Selecione pelo menos um Prestador de Serviços.", RecipientsField);
        }

        if (proposalModel == ProposalModel.Public && count > 0)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Um trabalho publicado não tem destinatários: não indique Prestadores.", RecipientsField);
        }
    }

    // RF-F009-01, rules 5 and 6 (UC03, E5.3): each chosen provider must have an ACTIVE account and the category of
    // the service in the profile. Returns one message per profile that cannot be chosen, naming the provider.
    // The profiles must be loaded with the provider and the areas. Without a category (the one indicated does not
    // exist), only the account is checked, so the other errors are still reported in the same response.
    public static IReadOnlyList<string> DescribeIneligible(
        IEnumerable<ProfessionalProfile> profiles, int? serviceCategoryId)
    {
        var messages = new List<string>();
        foreach (var profile in profiles)
        {
            string provider = $"O Prestador {profile.ServiceProvider.Name} (perfil {profile.ProfessionalProfileId})";
            if (profile.ServiceProvider.Status != ActivationStatus.Active)
            {
                messages.Add($"{provider} tem a conta desativada.");
            }
            else if (serviceCategoryId is not null
                     && profile.ServiceAreas.All(a => a.ServiceCategoryId != serviceCategoryId))
            {
                messages.Add($"{provider} não tem a categoria do serviço no perfil.");
            }
        }

        return messages;
    }
}