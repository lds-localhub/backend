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
public class DirectRequestService(LocalHubDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    // E23 (RF-F007-09), provider side: only the requests sent to the profile of the provider, newest first. The
    // proposal of the provider is the one for the same service. The data of the Client is not returned.
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

        var requests = db.DirectRequests.AsNoTracking().Where(r => r.ProfessionalProfileId == profileId);
        if (statusFilter is not null)
        {
            requests = requests.Where(r => r.Status == statusFilter);
        }

        int total = await requests.CountAsync(cancellationToken);
        var rows = await requests
            .OrderByDescending(r => r.CreationDate)
            .ThenByDescending(r => r.DirectRequestId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(r => new
            {
                r.DirectRequestId,
                r.Status,
                r.CreationDate,
                r.ResponseDate,
                Service = db.Services
                    .Where(s => s.ServiceId == r.ServiceId)
                    .Select(s => new DirectRequestServiceSummary(
                        s.ServiceId,
                        new ServiceCategorySummary(s.ServiceCategoryId, s.ServiceCategory.Name),
                        s.Description,
                        s.Location,
                        s.RequestedStartDate,
                        s.RequestedEndDate,
                        s.Status))
                    .First(),
                Proposal = db.Proposals
                    .Where(p => p.ServiceId == r.ServiceId && p.ProfessionalProfileId == r.ProfessionalProfileId)
                    .Select(p => new { p.ProposalId, p.Amount, p.Conditions, p.Obs, p.Status })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new DirectRequestListItem(
                r.DirectRequestId,
                r.Status,
                r.CreationDate,
                r.ResponseDate,
                r.Service,
                r.Proposal is null
                    ? null
                    : new DirectRequestProposalSummary(
                        r.Proposal.ProposalId, r.Proposal.Amount, r.Proposal.Conditions, r.Proposal.Obs, r.Proposal.Status)))
            .ToList();

        return new PagedResult<DirectRequestListItem>(page.Page, page.PageSize, total, items);
    }

    // E36 (RF-F007-09): one direct request, only for the provider that received it (another provider gets 403). There is
    // no history of states yet (I050), so history is empty.
    public async Task<ProviderDirectRequestResponse> GetAsync(int directRequestId, CancellationToken cancellationToken)
    {
        int profileId = await GetMyProfileIdAsync(cancellationToken);

        var row = await db.DirectRequests.AsNoTracking()
            .Where(r => r.DirectRequestId == directRequestId)
            .Select(r => new
            {
                r.ProfessionalProfileId,
                r.Status,
                r.CreationDate,
                r.ResponseDate,
                Service = db.Services
                    .Where(s => s.ServiceId == r.ServiceId)
                    .Select(s => new
                    {
                        s.ServiceId,
                        s.ServiceCategoryId,
                        CategoryName = s.ServiceCategory.Name,
                        s.Description,
                        s.Location,
                        s.Latitude,
                        s.Longitude,
                        s.RequestedStartDate,
                        s.RequestedEndDate,
                        s.Status,
                        s.CreationDate,
                        ClientName = s.Client.Name,
                    })
                    .First(),
                ProposalId = db.Proposals
                    .Where(p => p.ServiceId == r.ServiceId && p.ProfessionalProfileId == r.ProfessionalProfileId)
                    .Select(p => (int?)p.ProposalId)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Solicitação direta não encontrada.");

        if (row.ProfessionalProfileId != profileId)
        {
            throw new ForbiddenException("Não tem permissão para aceder a esta solicitação direta.");
        }

        var location = await db.FindLocationAsync(currentUser.UserId, cancellationToken);
        var service = row.Service;

        return new ProviderDirectRequestResponse(
            directRequestId,
            row.Status,
            row.CreationDate,
            row.ResponseDate,
            new DirectRequestServiceDetail(
                service.ServiceId,
                new ServiceCategorySummary(service.ServiceCategoryId, service.CategoryName),
                service.Description,
                service.Location,
                LocationQueries.DistanceKm(location, service.Latitude, service.Longitude),
                service.RequestedStartDate,
                service.RequestedEndDate,
                service.Status,
                service.CreationDate,
                service.ClientName,
                History: []),
            row.ProposalId);
    }

    // E24 (RF-F007-02). The rules (recipient, OPEN service, PENDING request) are in Service.DeclineDirectRequest.
    public async Task<DirectRequestStatusResponse> DeclineAsync(int directRequestId, CancellationToken cancellationToken)
    {
        int profileId = await GetMyProfileIdAsync(cancellationToken);
        var service = await db.Services
            .Include(s => s.DirectRequests)
            .SingleOrDefaultAsync(s => s.DirectRequests.Any(r => r.DirectRequestId == directRequestId), cancellationToken)
            ?? throw new NotFoundException("Solicitação direta não encontrada.");

        var utcNow = timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var request = service.DeclineDirectRequest(directRequestId, profileId, now);
        await db.SaveChangesAsync(cancellationToken);

        return DirectRequestStatusResponse.From(service.ServiceId, request);
    }

    private async Task<int> GetMyProfileIdAsync(CancellationToken cancellationToken)
    {
        return await db.FindProfileIdAsync(currentUser.UserId, cancellationToken)
               ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
    }
}