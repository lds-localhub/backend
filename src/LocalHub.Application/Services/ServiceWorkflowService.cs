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
using LocalHub.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Services;

// Creation and life cycle of the services, direct requests and proposals (F007 to F011).
public class ServiceWorkflowService(
    LocalHubDbContext db,
    LocationResolver locationResolver,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    // E20 (RF-F009-01, RF-F009-03, RF-F010-01). The checks go from the cheapest to the most expensive: first the
    // fields, then the category and the chosen profiles (database), and last the address (Nominatim). Nothing is
    // changed before the address is converted (rule R2), and the service and its direct requests are saved with a
    // single SaveChangesAsync, in one transaction (rule R1): on any error nothing is created.
    public async Task<ServiceResponse> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var client = await db.Users.SingleOrDefaultAsync(u => u.UserId == currentUser.UserId, cancellationToken)
                     ?? throw new NotFoundException("Utilizador não encontrado.");
        // PostgreSQL keeps microseconds (timestamptz), so the instant is truncated before it is used: the creationDate
        // returned now is the same that will be read from the database later (E21, E22).
        var utcNow = timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var profileIds = request.ProfessionalProfileIds?.Distinct().ToList() ?? [];

        var errors = new FieldErrors();
        CollectFieldErrors(errors, request, now);
        errors.ThrowIfAny();

        var category = await db.ServiceCategories
            .SingleOrDefaultAsync(c => c.ServiceCategoryId == request.ServiceCategoryId, cancellationToken);
        if (category is null)
        {
            errors.Add(Service.CategoryField, "A categoria indicada não existe.");
        }
        else
        {
            errors.Collect(() => Service.ValidateCategory(category), Service.CategoryField);
        }

        var profiles = await LoadProfilesAsync(profileIds, cancellationToken);
        var recipientErrors = profileIds
            .Where(id => profiles.All(p => p.ProfessionalProfileId != id))
            .Select(id => $"O perfil {id} não existe.")
            .ToList();
        recipientErrors.AddRange(Service.DescribeIneligible(profiles, category?.ServiceCategoryId));

        if (recipientErrors.Count > 0)
        {
            errors.Add(Service.RecipientsField, [.. recipientErrors]);
        }

        bool hasAddress = !string.IsNullOrWhiteSpace(request.Location);
        if (!hasAddress && (client.Address is null || client.Latitude is null || client.Longitude is null))
        {
            errors.Add(Service.LocationField,
                "Indique o local de realização: ainda não tem uma localização registada.");
        }

        errors.ThrowIfAny();

        // Without location, the client's registered location; otherwise the address converted into coordinates,
        // without changing the registered location (RF-F009-01, rule 3; RF-F010-01, rule 6).
        var location = hasAddress
            ? await locationResolver.ResolveAddressAsync(request.Location!, Service.LocationField, cancellationToken)
            : new ResolvedLocation(client.Address!, GeoPoint.Create(client.Latitude!.Value, client.Longitude!.Value));

        var service = Service.Create(
            client,
            category!,
            request.ProposalModel!.Value,
            request.Description,
            request.RequestedStartDate,
            request.RequestedEndDate,
            location.Address,
            location.Coordinates,
            profiles,
            now);

        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResponse.From(service);
    }

    // E25 (RF-F007-01, RF-F011-02). The fields are checked first (400); the access and the states are decided by
    // Service.SubmitProposal. The proposal, the answer of the direct request and the unique index (provider, service)
    // are saved with a single SaveChangesAsync (rule R1).
    public async Task<ProposalResponse> SubmitProposalAsync(
        int serviceId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var errors = new FieldErrors();
        errors.Collect(() => Proposal.ValidateAmount(request.Amount), Proposal.AmountField);
        errors.Collect(() => Proposal.ValidateConditions(request.Conditions), Proposal.ConditionsField);
        errors.Collect(() => Proposal.ValidateObs(request.Obs), Proposal.ObsField);
        errors.ThrowIfAny();

        var provider = await db.ProfessionalProfiles
            .Include(p => p.ServiceAreas)
            .SingleOrDefaultAsync(p => p.ServiceProviderId == currentUser.UserId, cancellationToken)
            ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        int profileId = provider.ProfessionalProfileId;

        var service = await db.Services
            .Include(s => s.Client)
            .Include(s => s.DirectRequests.Where(r => r.ProfessionalProfileId == profileId))
            .Include(s => s.Proposals.Where(p => p.ProfessionalProfileId == profileId))
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken)
            ?? throw new NotFoundException("Serviço não encontrado.");

        var utcNow = timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var proposal = service.SubmitProposal(provider, request.Amount, request.Conditions, request.Obs, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UQ_Proposal_professionalProfileId_serviceId"
        })
        {
            // Two simultaneous proposals of the same provider: the second one is refused by the unique index.
            throw new DomainException(DomainErrorKind.Conflict, "Já apresentou uma proposta a este serviço.");
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

        int profileId = await db.FindProfileIdAsync(currentUser.UserId, cancellationToken)
                        ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);

        var rows = db.Proposals.AsNoTracking()
            .Where(p => p.ProfessionalProfileId == profileId)
            .Join(db.Services, p => p.ServiceId, s => s.ServiceId, (p, s) => new { Proposal = p, Service = s });
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
        int profileId = await db.FindProfileIdAsync(currentUser.UserId, cancellationToken)
                        ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);

        var service = await db.Services.AsNoTracking()
            .Include(s => s.ServiceCategory)
            .Include(s => s.Client)
            .Include(s => s.DirectRequests.Where(r => r.ProfessionalProfileId == profileId))
            .Include(s => s.Proposals.Where(p => p.ProfessionalProfileId == profileId))
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken)
            ?? throw new NotFoundException("Serviço não encontrado.");

        bool isParticipant = service.DirectRequests.Count > 0 || service.Proposals.Count > 0;
        bool isOpenJob = service.ProposalModel == ProposalModel.Public
                         && service.Status == ServiceStatus.Open
                         && service.Client.Status == ActivationStatus.Active;
        if (!isParticipant && !isOpenJob)
        {
            throw new ForbiddenException("Não tem permissão para aceder a este serviço.");
        }

        var location = await db.Users.AsNoTracking()
            .Where(u => u.UserId == currentUser.UserId && u.Latitude != null && u.Longitude != null)
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
            History: []);
    }

    // E21 (RF-F008-01): the client sees the services it created; the provider, the services in which its proposal was
    // accepted. Newest first, filtered by state and modality. pendingProposalsCount is always 0 for the provider.
    public async Task<PagedResult<ServiceListItem>> ListMineAsync(
        string? status, string? proposalModel, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<ServiceStatus>(status, "status", errors);
        var modelFilter = EnumQuery.Parse<ProposalModel>(proposalModel, "proposalModel", errors);
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        bool isClient = currentUser.Role == UserRole.Client;
        IQueryable<Service> services = db.Services.AsNoTracking();
        if (isClient)
        {
            services = services.Where(s => s.ClientId == currentUser.UserId);
        }
        else
        {
            int profileId = await db.FindProfileIdAsync(currentUser.UserId, cancellationToken)
                            ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
            services = services.Where(s => s.Proposals.Any(
                p => p.ProfessionalProfileId == profileId && p.Status == ProposalStatus.Accepted));
        }

        if (statusFilter is not null)
        {
            services = services.Where(s => s.Status == statusFilter);
        }

        if (modelFilter is not null)
        {
            services = services.Where(s => s.ProposalModel == modelFilter);
        }

        int total = await services.CountAsync(cancellationToken);
        var items = await services
            .OrderByDescending(s => s.CreationDate)
            .ThenByDescending(s => s.ServiceId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(s => new ServiceListItem(
                s.ServiceId,
                s.ClientId,
                s.ProposalModel,
                new ServiceCategorySummary(s.ServiceCategoryId, s.ServiceCategory.Name),
                s.Description,
                s.RequestedStartDate,
                s.RequestedEndDate,
                s.Status,
                s.Location,
                s.Latitude,
                s.Longitude,
                s.CreationDate,
                s.ActualCompletionDate,
                isClient ? s.Proposals.Count(p => p.Status == ProposalStatus.Pending) : 0))
            .ToListAsync(cancellationToken);

        return new PagedResult<ServiceListItem>(page.Page, page.PageSize, total, items);
    }

    // E22 (RF-F007-09, RF-F008-01), client side: only the client that created the service, with all the direct
    // requests and the accepted proposal.
    public async Task<ClientServiceResponse> GetForClientAsync(int serviceId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(db.Services.AsNoTracking(), serviceId, cancellationToken);
        return ClientServiceResponse.From(service);
    }

    // E26 (RF-F007-09, RF-F008-01): all the proposals of the service, newest first, optionally filtered by state.
    // Only the PENDING proposals of an OPEN service can be accepted or declined (E28, E29).
    public async Task<IReadOnlyList<ClientProposalResponse>> ListProposalsAsync(
        int serviceId, string? status, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<ProposalStatus>(status, "status", errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var service = await FindOwnServiceAsync(db.Services.AsNoTracking(), serviceId, cancellationToken);
        return service.Proposals
            .Where(p => statusFilter is null || p.Status == statusFilter)
            .OrderByDescending(p => p.CreationDate)
            .ThenByDescending(p => p.ProposalId)
            .Select(ClientProposalResponse.From)
            .ToList();
    }

    // E28 (RF-F007-03, RF-F008-02). The changes of the service, of the proposals and of the direct requests are saved
    // with a single SaveChangesAsync (rule R1). Two simultaneous acceptances in the same service: the second one is
    // refused by xmin or by the index of the accepted proposal, and gets 409.
    public async Task<ClientServiceResponse> AcceptProposalAsync(
        int serviceId, int proposalId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(db.Services, serviceId, cancellationToken);
        EnsureProposalBelongs(service, proposalId);

        service.AcceptProposal(proposalId, currentUser.UserId, Now());
        await SaveDecisionAsync(cancellationToken);

        return ClientServiceResponse.From(service);
    }

    // E29 (RF-F007-04, RF-F008-03): only the proposal changes; the service stays OPEN.
    public async Task<ProposalResponse> DeclineProposalAsync(
        int serviceId, int proposalId, CancellationToken cancellationToken)
    {
        var service = await FindOwnServiceAsync(db.Services, serviceId, cancellationToken);
        EnsureProposalBelongs(service, proposalId);

        var proposal = service.DeclineProposal(proposalId, currentUser.UserId, Now());
        await SaveDecisionAsync(cancellationToken);

        return ProposalResponse.From(proposal);
    }

    // The service with everything the client sees (E22, E26, E28). 404 when it does not exist; 403 when it was created
    // by another client (API documentation, section 7).
    private async Task<Service> FindOwnServiceAsync(
        IQueryable<Service> services, int serviceId, CancellationToken cancellationToken)
    {
        var service = await services
            .Include(s => s.ServiceCategory)
            .Include(s => s.DirectRequests).ThenInclude(r => r.ProfessionalProfile).ThenInclude(p => p.ServiceProvider)
            .Include(s => s.Proposals).ThenInclude(p => p.ProfessionalProfile).ThenInclude(p => p.ServiceProvider)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken)
            ?? throw new NotFoundException("Serviço não encontrado.");

        if (service.ClientId != currentUser.UserId)
        {
            throw new ForbiddenException("Não tem permissão para aceder a este serviço.");
        }

        return service;
    }

    // A proposal of another service is reported as not found (API documentation, E28 and E29).
    private static void EnsureProposalBelongs(Service service, int proposalId)
    {
        if (service.Proposals.All(p => p.ProposalId != proposalId))
        {
            throw new NotFoundException("Proposta não encontrada.");
        }
    }

    // PostgreSQL keeps microseconds (timestamptz), so the instant is truncated before it is used.
    private DateTimeOffset Now()
    {
        var utcNow = timeProvider.GetUtcNow();
        return utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    // Rule R1: a service, a proposal or a direct request changed by another request after being read (xmin), or a
    // second accepted proposal in the service (UX_Proposal_serviceId_accepted), is not saved. The client has to
    // reload the service, so the error is a 409 transicao-invalida.
    private async Task SaveDecisionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, ChangedByAnotherRequestMessage);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ProposalConfiguration.AcceptedIndexName
        })
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, ChangedByAnotherRequestMessage);
        }
    }

    private const string ChangedByAnotherRequestMessage =
        "O serviço foi alterado por outro pedido entretanto. Recarregue-o e tente novamente.";

    // The rules that need neither the database nor the Nominatim, all reported in the same response.
    private static void CollectFieldErrors(FieldErrors errors, CreateServiceRequest request, DateTimeOffset now)
    {
        errors.Collect(() => Service.ValidateProposalModel(request.ProposalModel), Service.ProposalModelField);
        if (request.ServiceCategoryId is null)
        {
            errors.Add(Service.CategoryField, "Indique a categoria do serviço.");
        }

        errors.Collect(() => Service.ValidateDescription(request.Description), Service.DescriptionField);
        errors.Collect(
            () => Service.ValidateRequestedDates(
                request.RequestedStartDate, request.RequestedEndDate, Service.TodayInPortugal(now)),
            Service.StartDateField);
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            errors.Collect(() => Service.ValidateLocation(request.Location), Service.LocationField);
        }

        if (errors.Contains(Service.ProposalModelField))
        {
            return;
        }

        // PUBLIC does not accept the field at all, not even an empty list (API documentation, E20).
        if (request.ProposalModel == ProposalModel.Public && request.ProfessionalProfileIds is not null)
        {
            errors.Add(Service.RecipientsField,
                "Um trabalho publicado não tem destinatários: não indique professionalProfileIds.");
            return;
        }

        errors.Collect(
            () => Service.ValidateRecipientCount(request.ProposalModel!.Value, request.ProfessionalProfileIds?.Count ?? 0),
            Service.RecipientsField);
    }

    // With the provider (account state) and the categories of the profile (RF-F009-01, rules 5 and 6).
    private async Task<List<ProfessionalProfile>> LoadProfilesAsync(
        List<int> profileIds, CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return [];
        }

        return await db.ProfessionalProfiles
            .Include(p => p.ServiceProvider)
            .Include(p => p.ServiceAreas)
            .Where(p => profileIds.Contains(p.ProfessionalProfileId))
            .ToListAsync(cancellationToken);
    }
}