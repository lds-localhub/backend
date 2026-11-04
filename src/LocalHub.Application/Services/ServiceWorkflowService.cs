using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles;
using LocalHub.Application.Reviews;
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
    private const string AcceptedProposalIndex = "UX_Proposal_serviceId_accepted";

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
    public async Task<ClientServiceResponse> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var client = await _db.Users.SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (client == null)
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
        if (category == null)
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

        errors.ThrowIfAny();

        // Rule R15: the form shows the client's registered address; when location is that address, the registered
        // coordinates are used without calling the Nominatim. Otherwise the address is converted into coordinates,
        // without changing the registered location (RF-F009-01, rule 3; RF-F010-01, rule 6).
        string address = request.Location!.Trim();
        var location = address == client.Address && client.Latitude != null && client.Longitude != null
            ? new ResolvedLocation(client.Address, Coordinates.Rounded(client.Latitude.Value, client.Longitude.Value))
            : await _locationResolver.ResolveAddressAsync(address, LocationField, cancellationToken);

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

        // The response has the structure of E22 (API documentation, E20).
        return await ToClientResponseAsync(service, cancellationToken);
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
        if (provider == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        int profileId = provider.ProfessionalProfileId;

        var service = await _db.Services
            .Include(s => s.Client)
            .Include(s => s.DirectRequests.Where(r => r.ProfessionalProfileId == profileId))
            .Include(s => s.Proposals.Where(p => p.ProfessionalProfileId == profileId))
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service == null)
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

    // E27 (RF-F008-07): the proposals of the provider, newest first, optionally filtered by the state of the proposal,
    // with the summary that the lists show. Of the Client, only the name is returned, without any contacts.
    public async Task<PagedResult<MyProposalListItem>> ListMyProposalsAsync(
        string? status, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<ProposalStatus>(status, "status", errors);
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        var proposals = _db.Proposals.AsNoTracking().Where(p => p.ProfessionalProfileId == profileId);
        if (statusFilter != null)
        {
            proposals = proposals.Where(p => p.Status == statusFilter);
        }

        int total = await proposals.CountAsync(cancellationToken);
        var items = await proposals
            .OrderByDescending(p => p.CreationDate)
            .ThenByDescending(p => p.ProposalId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(p => new MyProposalListItem(
                p.ProposalId,
                p.Amount,
                p.Status,
                new ProposalServiceSummary(
                    p.ServiceId,
                    p.Service.Title,
                    new ServiceCategorySummary(p.Service.ServiceCategoryId, p.Service.ServiceCategory.Name),
                    p.Service.RequestedStartDate,
                    p.Service.RequestedEndDate,
                    p.Service.Status,
                    p.Service.Client.Name)))
            .ToListAsync(cancellationToken);

        return new PagedResult<MyProposalListItem>(page.Page, page.PageSize, total, items);
    }

    // E37 (RF-F008-07): one proposal, only for the provider that presented it (another provider gets 403).
    public async Task<ProviderProposalResponse> GetMyProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        var row = await _db.Proposals.AsNoTracking()
            .Where(p => p.ProposalId == proposalId)
            .Select(p => new
            {
                p.ProfessionalProfileId,
                p.Amount,
                p.Conditions,
                p.Obs,
                p.Status,
                p.CreationDate,
                p.DecisionDate,
                p.ServiceId,
                p.Service.ProposalModel,
                p.Service.ServiceCategoryId,
                CategoryName = p.Service.ServiceCategory.Name,
                p.Service.Title,
                p.Service.Description,
                p.Service.Location,
                p.Service.Latitude,
                p.Service.Longitude,
                p.Service.RequestedStartDate,
                p.Service.RequestedEndDate,
                ServiceStatus = p.Service.Status,
                ServiceCreationDate = p.Service.CreationDate,
                ClientName = p.Service.Client.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row == null)
        {
            throw new NotFoundException("Proposta não encontrada.");
        }

        if (row.ProfessionalProfileId != profileId.Value)
        {
            throw new ForbiddenException("Não tem permissão para aceder a esta proposta.");
        }

        var location = await _db.FindLocationAsync(_currentUser.UserId, cancellationToken);

        return new ProviderProposalResponse(
            proposalId,
            row.Amount,
            row.Conditions,
            row.Obs,
            row.Status,
            row.CreationDate,
            row.DecisionDate,
            new ProposalServiceDetail(
                row.ServiceId,
                row.ProposalModel,
                new ServiceCategorySummary(row.ServiceCategoryId, row.CategoryName),
                row.Title,
                row.Description,
                row.Location,
                LocationQueries.DistanceKm(location, row.Latitude, row.Longitude),
                row.RequestedStartDate,
                row.RequestedEndDate,
                row.ServiceStatus,
                row.ServiceCreationDate,
                row.ClientName));
    }

    // E21 (RF-F008-01): the services that the client created, newest first, optionally filtered by state, with the
    // summary that the lists show (counters, the provider of the accepted proposal and whether it was reviewed).
    public async Task<PagedResult<ServiceListItem>> ListMineAsync(
        string? status, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<ServiceStatus>(status, "status", errors);
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var services = _db.Services.AsNoTracking().Where(s => s.ClientId == _currentUser.UserId);
        if (statusFilter != null)
        {
            services = services.Where(s => s.Status == statusFilter);
        }

        int total = await services.CountAsync(cancellationToken);
        var items = await services
            .OrderByDescending(s => s.CreationDate)
            .ThenByDescending(s => s.ServiceId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(s => new ServiceListItem(
                s.ServiceId,
                s.Title,
                s.ProposalModel,
                new ServiceCategorySummary(s.ServiceCategoryId, s.ServiceCategory.Name),
                s.Status,
                s.CreationDate,
                s.DirectRequests.Count,
                s.Proposals.Count,
                s.Proposals.Count(p => p.Status == ProposalStatus.Pending),
                s.Proposals
                    .Where(p => p.Status == ProposalStatus.Accepted)
                    .Select(p => new AssignedProvider(p.ProfessionalProfileId, p.ProfessionalProfile.ServiceProvider.Name))
                    .FirstOrDefault(),
                s.Review != null))
            .ToListAsync(cancellationToken);

        return new PagedResult<ServiceListItem>(page.Page, page.PageSize, total, items);
    }

    // E22 (RF-F007-09, RF-F008-01): only the client that created the service, with all the direct requests (each with
    // its proposal) and, in PUBLIC, all the proposals received.
    public async Task<ClientServiceResponse> GetAsync(int serviceId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(_db.Services.AsNoTracking(), serviceId, cancellationToken);
        return await ToClientResponseAsync(service, cancellationToken);
    }

    // E28 (RF-F007-03, RF-F008-02). The changes of the service, of the proposals and of the direct requests are saved
    // with a single SaveChangesAsync (rule R1). Two simultaneous decisions in the same service: the second one waits
    // for the lock of the service, reads it already ASSIGNED and gets 409.
    public async Task<ProposalDecisionResponse> AcceptProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int serviceId = await FindServiceIdOfProposalAsync(proposalId, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockServiceAsync(serviceId, cancellationToken);

        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        var proposal = service.AcceptProposal(proposalId, _currentUser.UserId, Now());
        await SaveDecisionAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ProposalDecisionResponse.From(proposal);
    }

    // E29 (RF-F007-04, RF-F008-03): only the proposal changes; the service stays OPEN. It takes the same lock as E28,
    // so a proposal cannot be declined and accepted at the same time.
    public async Task<ProposalDecisionResponse> DeclineProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int serviceId = await FindServiceIdOfProposalAsync(proposalId, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockServiceAsync(serviceId, cancellationToken);

        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        var proposal = service.DeclineProposal(proposalId, _currentUser.UserId, Now());
        await SaveDecisionAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ProposalDecisionResponse.From(proposal);
    }

    // E30 (RF-F007-05, RF-F008-04), client side: the service, the proposals and the direct requests are saved with a
    // single SaveChangesAsync (rule R1). It takes the same lock as E28, so a service cannot be cancelled while a
    // proposal is being accepted.
    public async Task<ServiceStatusResponse> CancelAsClientAsync(int serviceId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockServiceAsync(serviceId, cancellationToken);

        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        service.CancelByClient(_currentUser.UserId, Now());
        await SaveDecisionAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceStatusResponse.From(service);
    }

    // E30 (RF-F007-05, RF-F008-04), provider side: only the provider whose proposal was accepted, while the service is
    // ASSIGNED.
    public async Task<ServiceStatusResponse> CancelAsProviderAsync(int serviceId, CancellationToken cancellationToken)
    {
        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockServiceAsync(serviceId, cancellationToken);

        // With the provider of each proposal, who is recorded in the history when the service is cancelled.
        var service = await _db.Services
            .Include(s => s.Proposals).ThenInclude(p => p.ProfessionalProfile).ThenInclude(p => p.ServiceProvider)
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new NotFoundException("Serviço não encontrado.");
        }

        service.CancelByProvider(profileId.Value, Now());
        await SaveDecisionAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceStatusResponse.From(service);
    }

    // E31 (RF-F007-06, RF-F008-05): only the client that created the service, while it is ASSIGNED. It takes the same
    // lock as E30, so a service cannot be completed and cancelled at the same time.
    public async Task<CompletedServiceResponse> CompleteAsync(int serviceId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockServiceAsync(serviceId, cancellationToken);

        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        service.Complete(_currentUser.UserId, Now());
        await SaveDecisionAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CompletedServiceResponse.From(service);
    }

    // Rule R1: the row of the service stays locked until the end of the transaction, so a second decision in the same
    // service waits for the first one and then reads the service already changed. Without the lock, both requests
    // would read the same PENDING proposals and the second one would silently overwrite the first.
    private async Task LockServiceAsync(int serviceId, CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM \"Service\" WHERE \"serviceId\" = {serviceId} FOR UPDATE", cancellationToken);
    }

    // The service with everything the client sees (E22), also used by E28 to E31. 404 when it does not exist; 403 when it was created
    // by another client (API documentation, section 7). The client is loaded, because it is recorded in the history of
    // the changes of state that it makes (E28, E30 and E31), and the history with the user of each change (E22).
    private async Task<Service> FindOwnServiceAsync(
        IQueryable<Service> services, int serviceId, CancellationToken cancellationToken)
    {
        var service = await services
            .Include(s => s.ServiceCategory)
            .Include(s => s.Client)
            .Include(s => s.Review)
            .Include(s => s.DirectRequests).ThenInclude(r => r.ProfessionalProfile).ThenInclude(p => p.ServiceProvider)
            .Include(s => s.Proposals).ThenInclude(p => p.ProfessionalProfile).ThenInclude(p => p.ServiceProvider)
            .Include(s => s.StatusHistory).ThenInclude(h => h.ChangedBy)
            // A single service has few direct requests, proposals and changes of state, so one query is enough.
            .AsSingleQuery()
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new NotFoundException("Serviço não encontrado.");
        }

        if (service.ClientId != _currentUser.UserId)
        {
            throw new ForbiddenException("Não tem permissão para aceder a este serviço.");
        }

        return service;
    }

    // The service of the proposal (E28 and E29): 404 when the proposal does not exist. Whether the service belongs to
    // the client is checked afterwards, in FindOwnServiceAsync (403).
    private async Task<int> FindServiceIdOfProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int? serviceId = await _db.Proposals
            .Where(p => p.ProposalId == proposalId)
            .Select(p => (int?)p.ServiceId)
            .SingleOrDefaultAsync(cancellationToken);
        if (serviceId == null)
        {
            throw new NotFoundException("Proposta não encontrada.");
        }

        return serviceId.Value;
    }

    // E22 as the client sees it, with the reviewCount of each provider of the direct requests and of the proposals.
    private async Task<ClientServiceResponse> ToClientResponseAsync(Service service, CancellationToken cancellationToken)
    {
        var profileIds = service.DirectRequests.Select(r => r.ProfessionalProfileId)
            .Concat(service.Proposals.Select(p => p.ProfessionalProfileId))
            .Distinct()
            .ToList();
        var reviewCounts = await _db.CountReviewsAsync(profileIds, cancellationToken);
        return ClientServiceResponse.From(service, reviewCounts);
    }

    // PostgreSQL keeps microseconds (timestamptz), so the instant is truncated before it is used.
    private DateTimeOffset Now()
    {
        var utcNow = _timeProvider.GetUtcNow();
        return utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    // Rule R1: besides the lock, a second accepted proposal in the same service is refused by the index
    // UX_Proposal_serviceId_accepted. The client has to reload the service, so the error is a 409 transicao-invalida.
    private async Task SaveDecisionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && postgres.ConstraintName == AcceptedProposalIndex)
            {
                throw new DomainException(DomainErrorKind.InvalidTransition,
                    "O serviço foi alterado por outro pedido entretanto. Recarregue-o e tente novamente.");
            }

            // Any other error of the database is not expected: the request gets 500.
            throw;
        }
    }

    // The rules that compare fields of the request, all reported in the same response.
    private static void CollectRequestErrors(FieldErrors errors, CreateServiceRequest request, DateTimeOffset now)
    {
        // RF-F010-01, rule 5: a single date (start only) or an interval. The start cannot be before today and the end
        // cannot be before the start. The current date is calculated in UTC (API documentation, section 4.3).
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        if (request.RequestedStartDate < today)
        {
            errors.Add(StartDateField, "A data pretendida tem de ser igual ou posterior à data de hoje.");
        }
        else if (request.RequestedEndDate != null && request.RequestedEndDate < request.RequestedStartDate)
        {
            errors.Add(EndDateField, "A data de fim tem de ser igual ou posterior à data de início.");
        }

        // PUBLIC does not accept the field at all, not even an empty list (API documentation, E20).
        if (request.ProposalModel == ProposalModel.Public && request.ProfessionalProfileIds != null)
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
            else if (serviceCategoryId != null
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