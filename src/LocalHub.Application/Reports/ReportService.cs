using LocalHub.Application.Common;
using LocalHub.Application.Reports.Dtos;
using LocalHub.Application.Reviews;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Reports;

// Activity reports: F013 (E33) and F014 (E34).
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

    // E33 (RF-F013-01, RF-F013-02). Only the services of the authenticated client (rule 5), with an ACCEPTED proposal
    // (rule 1: they reached ASSIGNED, whatever the current state) and created in the period. A service has at most one
    // ACCEPTED proposal (index UX_Proposal_serviceId_accepted), so each one is read with it and the indicators are
    // calculated by ClientReport.
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

    // E34 (RF-F014-01, RF-F014-02). Only the data of the authenticated provider (rule 6); a provider without a profile
    // is stopped before, by the filter (403 perfil-em-falta). The direct requests, the proposals and the accepted
    // amounts follow the creation date of their service (RF-F014-02, rule 2). The indicators are calculated by
    // ProviderReport.
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

    // The services created in the period (RF-F013-02, RF-F014-02), both dates included.
    private IQueryable<Service> CreatedIn(ReportPeriod period)
    {
        var services = _db.Services.AsNoTracking();
        if (period.From is not null)
        {
            var from = period.From.Value;
            services = services.Where(s => s.CreationDate >= from);
        }

        if (period.Until is not null)
        {
            var until = period.Until.Value;
            services = services.Where(s => s.CreationDate < until);
        }

        return services;
    }
}