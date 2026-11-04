using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Services;

// Direct requests seen and answered by the provider that received them (F007).
public class DirectRequestService
{
    private readonly LocalHubDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public DirectRequestService(LocalHubDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // E23 (RF-F007-09), provider side: only the requests sent to the profile of the provider, newest first, with the
    // summary that the lists show. The proposal of the provider is the one for the same service. Of the Client, only
    // the name is returned, without any contacts.
    public async Task<PagedResult<DirectRequestListItem>> ListAsync(
        string? status, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<DirectRequestStatus>(status, "status", errors);
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        int profileId = await GetMyProfileIdAsync(cancellationToken);

        var requests = _db.DirectRequests.AsNoTracking().Where(r => r.ProfessionalProfileId == profileId);
        if (statusFilter != null)
        {
            requests = requests.Where(r => r.Status == statusFilter);
        }

        int total = await requests.CountAsync(cancellationToken);
        var items = await requests
            .OrderByDescending(r => r.CreationDate)
            .ThenByDescending(r => r.DirectRequestId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(r => new DirectRequestListItem(
                r.DirectRequestId,
                r.Status,
                r.Service.Proposals
                    .Where(p => p.ProfessionalProfileId == r.ProfessionalProfileId)
                    .Select(p => (int?)p.ProposalId)
                    .FirstOrDefault(),
                new DirectRequestServiceSummary(
                    r.ServiceId,
                    r.Service.Title,
                    new ServiceCategorySummary(r.Service.ServiceCategoryId, r.Service.ServiceCategory.Name),
                    r.Service.RequestedStartDate,
                    r.Service.RequestedEndDate,
                    r.Service.Client.Name)))
            .ToListAsync(cancellationToken);

        return new PagedResult<DirectRequestListItem>(page.Page, page.PageSize, total, items);
    }

    // E36 (RF-F007-09): one direct request, only for the provider that received it (another provider gets 403). The
    // history of states of the service is in chronological order, only with the role of the author of each change, so
    // the hired provider is not identified (RNF-06).
    public async Task<ProviderDirectRequestResponse> GetAsync(int directRequestId, CancellationToken cancellationToken)
    {
        int profileId = await GetMyProfileIdAsync(cancellationToken);

        var row = await _db.DirectRequests.AsNoTracking()
            .Where(r => r.DirectRequestId == directRequestId)
            .Select(r => new
            {
                r.ProfessionalProfileId,
                r.Status,
                r.CreationDate,
                r.ResponseDate,
                Service = new
                {
                    r.ServiceId,
                    r.Service.ServiceCategoryId,
                    CategoryName = r.Service.ServiceCategory.Name,
                    r.Service.Title,
                    r.Service.Description,
                    r.Service.Location,
                    r.Service.Latitude,
                    r.Service.Longitude,
                    r.Service.RequestedStartDate,
                    r.Service.RequestedEndDate,
                    r.Service.Status,
                    r.Service.CreationDate,
                    ClientName = r.Service.Client.Name,
                },
                ProposalId = r.Service.Proposals
                    .Where(p => p.ProfessionalProfileId == r.ProfessionalProfileId)
                    .Select(p => (int?)p.ProposalId)
                    .FirstOrDefault(),
                History = r.Service.StatusHistory
                    .OrderBy(h => h.ChangedAt)
                    .ThenBy(h => h.ServiceStatusHistoryId)
                    .Select(h => new ProviderServiceHistoryResponse(
                        h.PreviousStatus, h.NewStatus, h.ChangedAt, h.ChangedBy.Role))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row == null)
        {
            throw new NotFoundException("Solicitação direta não encontrada.");
        }

        if (row.ProfessionalProfileId != profileId)
        {
            throw new ForbiddenException("Não tem permissão para aceder a esta solicitação direta.");
        }

        var location = await _db.FindLocationAsync(_currentUser.UserId, cancellationToken);
        var service = row.Service;

        return new ProviderDirectRequestResponse(
            directRequestId,
            row.Status,
            row.CreationDate,
            row.ResponseDate,
            new DirectRequestServiceDetail(
                service.ServiceId,
                new ServiceCategorySummary(service.ServiceCategoryId, service.CategoryName),
                service.Title,
                service.Description,
                service.Location,
                LocationQueries.DistanceKm(location, service.Latitude, service.Longitude),
                service.RequestedStartDate,
                service.RequestedEndDate,
                service.Status,
                service.CreationDate,
                service.ClientName,
                row.History),
            row.ProposalId);
    }

    // E24 (RF-F007-02). The rules (recipient, OPEN service, PENDING request) are in Service.DeclineDirectRequest.
    public async Task<DirectRequestStatusResponse> DeclineAsync(int directRequestId, CancellationToken cancellationToken)
    {
        int profileId = await GetMyProfileIdAsync(cancellationToken);
        var service = await _db.Services
            .Include(s => s.DirectRequests)
            .SingleOrDefaultAsync(s => s.DirectRequests.Any(r => r.DirectRequestId == directRequestId), cancellationToken);
        if (service == null)
        {
            throw new NotFoundException("Solicitação direta não encontrada.");
        }

        var utcNow = _timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var request = service.DeclineDirectRequest(directRequestId, profileId, now);
        await _db.SaveChangesAsync(cancellationToken);

        return DirectRequestStatusResponse.From(request);
    }

    private async Task<int> GetMyProfileIdAsync(CancellationToken cancellationToken)
    {
        int? profileId = await _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);
        if (profileId == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        return profileId.Value;
    }
}