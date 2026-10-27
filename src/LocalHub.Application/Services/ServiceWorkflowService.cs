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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
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

    // E37 (RF-F008-07): one proposal, only for the provider that presented it (another provider gets 403).
    public async Task<ProviderProposalResponse> GetMyProposalAsync(int proposalId, CancellationToken cancellationToken)
    {
        int profileId = await db.FindProfileIdAsync(currentUser.UserId, cancellationToken)
                        ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);

        var row = await db.Proposals.AsNoTracking()
            .Where(p => p.ProposalId == proposalId)
            .Join(db.Services, p => p.ServiceId, s => s.ServiceId, (p, s) => new
            {
                p.ProfessionalProfileId,
                p.Amount,
                p.Conditions,
                p.Obs,
                p.Status,
                p.CreationDate,
                p.DecisionDate,
                s.ServiceId,
                s.ProposalModel,
                s.ServiceCategoryId,
                CategoryName = s.ServiceCategory.Name,
                s.Description,
                s.Location,
                s.Latitude,
                s.Longitude,
                s.RequestedStartDate,
                s.RequestedEndDate,
                ServiceStatus = s.Status,
                ServiceCreationDate = s.CreationDate,
                ClientName = s.Client.Name,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Proposta não encontrada.");

        if (row.ProfessionalProfileId != profileId)
        {
            throw new ForbiddenException("Não tem permissão para aceder a esta proposta.");
        }

        var location = await db.FindLocationAsync(currentUser.UserId, cancellationToken);

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
                row.Description,
                row.Location,
                LocationQueries.DistanceKm(location, row.Latitude, row.Longitude),
                row.RequestedStartDate,
                row.RequestedEndDate,
                row.ServiceStatus,
                row.ServiceCreationDate,
                row.ClientName));
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