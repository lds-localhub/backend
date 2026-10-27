using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Geocoding;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Services;

// Creation and life cycle of the services, direct requests and proposals (F007 to F011).
public class ServiceWorkflowService
{
    private const string CategoryField = "serviceCategoryId";
    private const string StartDateField = "requestedStartDate";
    private const string EndDateField = "requestedEndDate";
    private const string LocationField = "location";
    private const string RecipientsField = "professionalProfileIds";

    // The requested dates are compared with the date in Portugal (RF-F010-01, rule 5).
    private static readonly TimeZoneInfo Portugal = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly LocalHubDbContext _db;
    private readonly LocationResolver _locationResolver;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ServiceWorkflowService(
        LocalHubDbContext db,
        LocationResolver locationResolver,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _db = db;
        _locationResolver = locationResolver;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // E20 (RF-F009-01, RF-F009-03, RF-F010-01). The fields were validated by the DTO; here are the rules that compare
    // fields or need the database or the Nominatim (rule R17). The checks go from the cheapest to the most expensive:
    // first the dates and the recipients, then the category and the chosen profiles (database), and last the address
    // (Nominatim). Nothing is changed before the address is converted (rule R2), and the service and its direct
    // requests are saved with a single SaveChangesAsync, in one transaction (rule R1): on any error nothing is created.
    public async Task<ServiceResponse> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var client = await _db.Users.SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (client is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        // PostgreSQL keeps microseconds (timestamptz), so the instant is truncated before it is used: the creationDate
        // returned now is the same that will be read from the database later (E21, E22).
        var utcNow = _timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var profileIds = request.ProfessionalProfileIds?.Distinct().ToList() ?? new List<int>();

        var errors = new FieldErrors();
        CollectRequestErrors(errors, request, now);
        errors.ThrowIfAny();

        var category = await _db.ServiceCategories
            .SingleOrDefaultAsync(c => c.ServiceCategoryId == request.ServiceCategoryId, cancellationToken);
        if (category is null)
        {
            errors.Add(CategoryField, "A categoria indicada não existe.");
        }

        else if (category.Status != ActivationStatus.Active)
        {
            // RF-F003-03, rule 3: a deactivated category cannot be used in new services.
            errors.Add(CategoryField, $"A categoria «{category.Name}» está desativada. Escolha uma categoria ativa.");
        }

        var profiles = await LoadProfilesAsync(profileIds, cancellationToken);
        var recipientErrors = profileIds
            .Where(id => profiles.All(p => p.ProfessionalProfileId != id))
            .Select(id => $"O perfil {id} não existe.")
            .ToList();
        recipientErrors.AddRange(DescribeIneligible(profiles, category?.ServiceCategoryId));

        if (recipientErrors.Count > 0)
        {
            errors.Add(RecipientsField, recipientErrors.ToArray());
        }

        bool hasAddress = !string.IsNullOrWhiteSpace(request.Location);
        if (!hasAddress && (client.Address is null || client.Latitude is null || client.Longitude is null))
        {
            errors.Add(LocationField, "Indique o local de realização: ainda não tem uma localização registada.");
        }

        errors.ThrowIfAny();

        // Without location, the client's registered location; otherwise the address converted into coordinates,
        // without changing the registered location (RF-F009-01, rule 3; RF-F010-01, rule 6).
        var location = hasAddress
            ? await _locationResolver.ResolveAddressAsync(request.Location!, LocationField, cancellationToken)
            : new ResolvedLocation(client.Address!, Coordinates.Rounded(client.Latitude!.Value, client.Longitude!.Value));

        var service = Service.Create(
            client,
            category!,
            request.ProposalModel!.Value,
            request.Title!,
            request.Description!,
            request.RequestedStartDate!.Value,
            request.RequestedEndDate,
            location.Address,
            location.Coordinates.Latitude,
            location.Coordinates.Longitude,
            profiles,
            now);

        _db.Services.Add(service);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResponse.From(service);
    }

    // E25 (RF-F007-01, RF-F011-02). The fields were validated by the DTO (rule R17); the access and the states are
    // decided by Service.SubmitProposal. The proposal, the answer of the direct request and the unique index
    // (provider, service) are saved with a single SaveChangesAsync (rule R1).
    public async Task<ProposalResponse> SubmitProposalAsync(
        int serviceId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.ProfessionalProfiles
            .Include(p => p.ServiceAreas)
            .SingleOrDefaultAsync(p => p.ServiceProviderId == _currentUser.UserId, cancellationToken);
        if (provider is null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        int profileId = provider.ProfessionalProfileId;

        var service = await _db.Services
            .Include(s => s.Client)
            .Include(s => s.DirectRequests.Where(r => r.ProfessionalProfileId == profileId))
            .Include(s => s.Proposals.Where(p => p.ProfessionalProfileId == profileId))
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException("Serviço não encontrado.");
        }

        // Optional: a missing or blank text is not saved.
        string? obs = string.IsNullOrWhiteSpace(request.Obs) ? null : request.Obs;

        var utcNow = _timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var proposal = service.SubmitProposal(provider, request.Amount!.Value, request.Conditions!, obs, now);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two simultaneous proposals of the same provider: the second one is refused by the unique index.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainException(DomainErrorKind.Conflict, "Já apresentou uma proposta a este serviço.");
            }

            // Any other error of the database is not expected: the request gets 500.
            throw;
        }

        return ProposalResponse.From(proposal);
    }

    // E27 (RF-F008-07): the proposals of the provider, newest first, filtered by the state of the proposal and by the
    // modality of the service.
    public async Task<PagedResult<MyProposalListItem>> ListMyProposalsAsync(
        string? status, string? proposalModel, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<ProposalStatus>(status, "status", errors);
        var modelFilter = EnumQuery.Parse<ProposalModel>(proposalModel, "proposalModel", errors);
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId is null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        var rows = _db.Proposals.AsNoTracking()
            .Where(p => p.ProfessionalProfileId == profileId)
            .Join(_db.Services, p => p.ServiceId, s => s.ServiceId, (p, s) => new { Proposal = p, Service = s });
        if (statusFilter is not null)
        {
            rows = rows.Where(r => r.Proposal.Status == statusFilter);
        }

        if (modelFilter is not null)
        {
            rows = rows.Where(r => r.Service.ProposalModel == modelFilter);
        }

        int total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(r => r.Proposal.CreationDate)
            .ThenByDescending(r => r.Proposal.ProposalId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(r => new MyProposalListItem(
                r.Proposal.ProposalId,
                r.Proposal.ServiceId,
                r.Proposal.ProfessionalProfileId,
                r.Proposal.Amount,
                r.Proposal.Conditions,
                r.Proposal.Obs,
                r.Proposal.Status,
                r.Proposal.CreationDate,
                r.Proposal.DecisionDate,
                new ProposalServiceSummary(
                    r.Service.ServiceId,
                    r.Service.ProposalModel,
                    new ServiceCategorySummary(r.Service.ServiceCategoryId, r.Service.ServiceCategory.Name),
                    r.Service.Title,
                    r.Service.Description,
                    r.Service.Location,
                    r.Service.RequestedStartDate,
                    r.Service.RequestedEndDate,
                    r.Service.Status)))
            .ToListAsync(cancellationToken);

        return new PagedResult<MyProposalListItem>(page.Page, page.PageSize, total, items);
    }

    // E22 (RF-F007-09, RF-F008-01, RF-F011-01), provider side. Access: the provider has a direct request or a proposal
    // in the service, or the service is a PUBLIC job that is OPEN and whose Client is ACTIVE. The response has only the
    // own direct request and proposal, and no data of the Client (RNF-06). There are no reviews or history of states yet
    // (later issues), so reviewed is false and history is empty.
    public async Task<ProviderServiceResponse> GetForProviderAsync(int serviceId, CancellationToken cancellationToken)
    {
        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId is null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        var service = await _db.Services.AsNoTracking()
            .Include(s => s.ServiceCategory)
            .Include(s => s.Client)
            .Include(s => s.DirectRequests.Where(r => r.ProfessionalProfileId == profileId))
            .Include(s => s.Proposals.Where(p => p.ProfessionalProfileId == profileId))
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException("Serviço não encontrado.");
        }

        bool isParticipant = service.DirectRequests.Count > 0 || service.Proposals.Count > 0;
        bool isOpenJob = service.ProposalModel == ProposalModel.Public
                         && service.Status == ServiceStatus.Open
                         && service.Client.Status == ActivationStatus.Active;
        if (!isParticipant && !isOpenJob)
        {
            throw new ForbiddenException("Não tem permissão para aceder a este serviço.");
        }

        var location = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId && u.Latitude != null && u.Longitude != null)
            .Select(u => new { Latitude = u.Latitude!.Value, Longitude = u.Longitude!.Value })
            .SingleOrDefaultAsync(cancellationToken);
        decimal? distanceKm = location is null
            ? null
            : GeoDistance.RoundKm(
                GeoDistance.HaversineKm(location.Latitude, location.Longitude, service.Latitude, service.Longitude));

        var myProposal = service.Proposals.SingleOrDefault();
        var myResponse = myProposal is null ? null : MyProposalResponse.From(myProposal);

        return new ProviderServiceResponse(
            service.ServiceId,
            service.ProposalModel,
            new ServiceCategorySummary(service.ServiceCategoryId, service.ServiceCategory.Name),
            service.Title,
            service.Description,
            service.RequestedStartDate,
            service.RequestedEndDate,
            service.Status,
            service.Location,
            service.Latitude,
            service.Longitude,
            distanceKm,
            service.CreationDate,
            service.ActualCompletionDate,
            Reviewed: false,
            service.DirectRequests.Select(DirectRequestResponse.From).ToList(),
            myResponse,
            myProposal?.Status == ProposalStatus.Accepted ? myResponse : null,
            History: new List<ServiceHistoryResponse>());
    }

    // The rules that compare fields of the request, all reported in the same response.
    private static void CollectRequestErrors(FieldErrors errors, CreateServiceRequest request, DateTimeOffset now)
    {
        // RF-F010-01, rule 5: a single date (start only) or an interval. The start cannot be before today and the end
        // cannot be before the start.
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Portugal).DateTime);
        if (request.RequestedStartDate < today)
        {
            errors.Add(StartDateField, "A data pretendida tem de ser igual ou posterior à data de hoje.");
        }
        else if (request.RequestedEndDate is not null && request.RequestedEndDate < request.RequestedStartDate)
        {
            errors.Add(EndDateField, "A data de fim tem de ser igual ou posterior à data de início.");
        }

        // PUBLIC does not accept the field at all, not even an empty list (API documentation, E20).
        if (request.ProposalModel == ProposalModel.Public && request.ProfessionalProfileIds is not null)
        {
            errors.Add(RecipientsField,
                "Um trabalho publicado não tem destinatários: não indique professionalProfileIds.");
        }

        // DIRECT_REQUEST needs at least one provider (RF-F009-01, rule 4).
        if (request.ProposalModel == ProposalModel.DirectRequest && (request.ProfessionalProfileIds?.Count ?? 0) == 0)
        {
            errors.Add(RecipientsField, "Selecione pelo menos um Prestador de Serviços.");
        }
    }

    // RF-F009-01, rules 5 and 6 (UC03, E5.3): each chosen provider must have an ACTIVE account and the category of
    // the service in the profile. Returns one message per profile that cannot be chosen, naming the provider.
    // Without a category (the one indicated does not exist), only the account is checked, so the other errors are
    // still reported in the same response.
    private static List<string> DescribeIneligible(IEnumerable<ProfessionalProfile> profiles, int? serviceCategoryId)
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

    // With the provider (account state) and the categories of the profile (RF-F009-01, rules 5 and 6).
    private async Task<List<ProfessionalProfile>> LoadProfilesAsync(
        List<int> profileIds, CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return new List<ProfessionalProfile>();
        }

        return await _db.ProfessionalProfiles
            .Include(p => p.ServiceProvider)
            .Include(p => p.ServiceAreas)
            .Where(p => profileIds.Contains(p.ProfessionalProfileId))
            .ToListAsync(cancellationToken);
    }
}