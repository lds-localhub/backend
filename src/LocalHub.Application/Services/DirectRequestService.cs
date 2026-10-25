using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Services;

// Direct requests seen and answered by the provider that received them, and seen by the client that sent them (F007).
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

    // E23 (RF-F007-09), client side: the requests of the services created by the client, newest first, each with the
    // provider and its proposal for the same service, so that the client can accept or decline it (E28, E29).
    public async Task<PagedResult<ClientDirectRequestListItem>> ListForClientAsync(
        string? status, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var statusFilter = EnumQuery.Parse<DirectRequestStatus>(status, "status", errors);
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        int clientId = currentUser.UserId;
        var requests = db.DirectRequests.AsNoTracking()
            .Where(r => db.Services.Any(s => s.ServiceId == r.ServiceId && s.ClientId == clientId));
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
                Provider = new ProviderSummary(
                    r.ProfessionalProfileId, r.ProfessionalProfile.ServiceProvider.Name, r.ProfessionalProfile.AverageRating),
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
            .Select(r => new ClientDirectRequestListItem(
                r.DirectRequestId,
                r.Status,
                r.CreationDate,
                r.ResponseDate,
                r.Provider,
                r.Service,
                r.Proposal is null
                    ? null
                    : new DirectRequestProposalSummary(
                        r.Proposal.ProposalId, r.Proposal.Amount, r.Proposal.Conditions, r.Proposal.Obs, r.Proposal.Status)))
            .ToList();

        return new PagedResult<ClientDirectRequestListItem>(page.Page, page.PageSize, total, items);
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