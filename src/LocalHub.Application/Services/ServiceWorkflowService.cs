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

    // Cheapest checks first and the address last, because it calls the Nominatim. Nothing is changed before that,
    // and everything is saved in a single SaveChangesAsync.
    public async Task<ClientServiceResponse> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var client = await _db.Users.SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (client == null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        var now = _timeProvider.GetUtcNow();
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

        // When the location is the registered address, reuse its coordinates instead of calling the Nominatim.
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

        return await ToClientResponseAsync(service, cancellationToken);
    }

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

        string? obs = string.IsNullOrWhiteSpace(request.Obs) ? null : request.Obs;

        var now = _timeProvider.GetUtcNow();
        var proposal = service.SubmitProposal(provider, request.Amount!.Value, request.Conditions!, obs, now);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two proposals at the same time: the unique index rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainException(DomainErrorKind.Conflict, "Já apresentou uma proposta a este serviço.");
            }

            throw;
        }

        return ProposalResponse.From(proposal);
    }

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

    public async Task<ClientServiceResponse> GetAsync(int serviceId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(_db.Services.AsNoTracking(), serviceId, cancellationToken);
        return await ToClientResponseAsync(service, cancellationToken);
    }

    // Saved in a single SaveChangesAsync; the unique index allows only one accepted proposal per service.
    public async Task<ProposalDecisionResponse> AcceptProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int serviceId = await FindServiceIdOfProposalAsync(proposalId, cancellationToken);

        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        var proposal = service.AcceptProposal(proposalId, _currentUser.UserId, _timeProvider.GetUtcNow());
        await SaveDecisionAsync(cancellationToken);

        return ProposalDecisionResponse.From(proposal);
    }

    public async Task<ProposalDecisionResponse> DeclineProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int serviceId = await FindServiceIdOfProposalAsync(proposalId, cancellationToken);

        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        var proposal = service.DeclineProposal(proposalId, _currentUser.UserId, _timeProvider.GetUtcNow());
        await SaveDecisionAsync(cancellationToken);

        return ProposalDecisionResponse.From(proposal);
    }

    public async Task<ServiceStatusResponse> CancelAsClientAsync(int serviceId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        service.CancelByClient(_currentUser.UserId, _timeProvider.GetUtcNow());
        await SaveDecisionAsync(cancellationToken);

        return ServiceStatusResponse.From(service);
    }

    public async Task<ServiceStatusResponse> CancelAsProviderAsync(int serviceId, CancellationToken cancellationToken)
    {
        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        var service = await _db.Services
            .Include(s => s.Proposals).ThenInclude(p => p.ProfessionalProfile).ThenInclude(p => p.ServiceProvider)
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new NotFoundException("Serviço não encontrado.");
        }

        service.CancelByProvider(profileId.Value, _timeProvider.GetUtcNow());
        await SaveDecisionAsync(cancellationToken);

        return ServiceStatusResponse.From(service);
    }

    public async Task<CompletedServiceResponse> CompleteAsync(int serviceId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(_db.Services, serviceId, cancellationToken);
        service.Complete(_currentUser.UserId, _timeProvider.GetUtcNow());
        await SaveDecisionAsync(cancellationToken);

        return CompletedServiceResponse.From(service);
    }

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
            // Few rows per service, so a single query is fine.
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

    // Ownership is checked afterwards, in FindOwnServiceAsync.
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

    private async Task<ClientServiceResponse> ToClientResponseAsync(Service service, CancellationToken cancellationToken)
    {
        var profileIds = service.DirectRequests.Select(r => r.ProfessionalProfileId)
            .Concat(service.Proposals.Select(p => p.ProfessionalProfileId))
            .Distinct()
            .ToList();
        var reviewCounts = await _db.CountReviewsAsync(profileIds, cancellationToken);
        return ClientServiceResponse.From(service, reviewCounts);
    }

    // A second accepted proposal is rejected by the unique index, so the client must reload the service.
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

            throw;
        }
    }

    private static void CollectRequestErrors(FieldErrors errors, CreateServiceRequest request, DateTimeOffset now)
    {
        // Today in UTC.
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        if (request.RequestedStartDate < today)
        {
            errors.Add(StartDateField, "A data pretendida tem de ser igual ou posterior à data de hoje.");
        }
        else if (request.RequestedEndDate != null && request.RequestedEndDate < request.RequestedStartDate)
        {
            errors.Add(EndDateField, "A data de fim tem de ser igual ou posterior à data de início.");
        }

        if (request.ProposalModel == ProposalModel.Public && request.ProfessionalProfileIds?.Count > 0)
        {
            errors.Add(RecipientsField,
                "Um trabalho publicado não tem destinatários: não indique professionalProfileIds.");
        }

        if (request.ProposalModel == ProposalModel.DirectRequest && (request.ProfessionalProfileIds?.Count ?? 0) == 0)
        {
            errors.Add(RecipientsField, "Selecione pelo menos um Prestador de Serviços.");
        }
    }

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