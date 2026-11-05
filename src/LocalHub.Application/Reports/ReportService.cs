using LocalHub.Application.Common;
using LocalHub.Application.Reports.Dtos;
using LocalHub.Application.Reviews;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Reports;

public class ReportService
{
    private readonly LocalHubDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReportService(LocalHubDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ClientReportResponse> GetClientReportAsync(
        string? startDate, string? endDate, CancellationToken cancellationToken)
    {
        var period = ReportPeriod.Parse(startDate, endDate);

        var contracted = await CreatedIn(period)
            .Where(s => s.ClientId == _currentUser.UserId
                        && s.Proposals.Any(p => p.Status == ProposalStatus.Accepted))
            .Select(s => new
            {
                s.ServiceId,
                s.ServiceCategoryId,
                CategoryName = s.ServiceCategory.Name,
                s.Status,
                s.ActualCompletionDate,
                Accepted = s.Proposals
                    .Where(p => p.Status == ProposalStatus.Accepted)
                    .Select(p => new { p.Amount, ProviderName = p.ProfessionalProfile.ServiceProvider.Name })
                    .First(),
            })
            .ToListAsync(cancellationToken);

        return ClientReport.Build(contracted
            .Select(s => new ContractedService(
                s.ServiceId,
                s.ServiceCategoryId,
                s.CategoryName,
                s.Status,
                s.ActualCompletionDate,
                s.Accepted.Amount,
                s.Accepted.ProviderName))
            .ToList());
    }

    // The direct requests, proposals and amounts follow the creation date of their service.
    public async Task<ProviderReportResponse> GetProviderReportAsync(
        string? startDate, string? endDate, CancellationToken cancellationToken)
    {
        var period = ReportPeriod.Parse(startDate, endDate);

        int professionalProfileId = await _db.ProfessionalProfiles.AsNoTracking()
            .Where(p => p.ServiceProviderId == _currentUser.UserId)
            .Select(p => p.ProfessionalProfileId)
            .SingleAsync(cancellationToken);

        var services = CreatedIn(period);

        var contracted = await services
            .SelectMany(s => s.Proposals)
            .Where(p => p.ProfessionalProfileId == professionalProfileId && p.Status == ProposalStatus.Accepted)
            .Select(p => new ProviderContractedService(
                p.Service.ServiceCategoryId, p.Service.ServiceCategory.Name, p.Service.Status, p.Amount))
            .ToListAsync(cancellationToken);

        int directRequestsReceived = await services
            .SelectMany(s => s.DirectRequests)
            .CountAsync(r => r.ProfessionalProfileId == professionalProfileId, cancellationToken);

        int declinedProposals = await services
            .SelectMany(s => s.Proposals)
            .CountAsync(
                p => p.ProfessionalProfileId == professionalProfileId && p.Status == ProposalStatus.Declined,
                cancellationToken);

        var reviews = await _db.ReviewsOf(professionalProfileId).AsNoTracking()
            .Select(r => new ReceivedReview(r.CreationDate, r.Rating))
            .ToListAsync(cancellationToken);

        return ProviderReport.Build(
            contracted,
            directRequestsReceived,
            declinedProposals,
            reviews,
            period,
            DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime));
    }

    private IQueryable<Service> CreatedIn(ReportPeriod period)
    {
        var services = _db.Services.AsNoTracking();
        if (period.From != null)
        {
            var from = period.From.Value;
            services = services.Where(s => s.CreationDate >= from);
        }

        if (period.Until != null)
        {
            var until = period.Until.Value;
            services = services.Where(s => s.CreationDate < until);
        }

        return services;
    }
}