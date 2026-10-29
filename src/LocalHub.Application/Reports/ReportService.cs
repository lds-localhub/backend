using LocalHub.Application.Common;
using LocalHub.Application.Reports.Dtos;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Reports;

// Activity reports: F013 (E33).
public class ReportService
{
    private readonly LocalHubDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReportService(LocalHubDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // E33 (RF-F013-01, RF-F013-02). Only the services of the authenticated client (rule 5), with an ACCEPTED proposal
    // (rule 1: they reached ASSIGNED, whatever the current state) and created in the period. A service has at most one
    // ACCEPTED proposal (index UX_Proposal_serviceId_accepted), so each one is read with it and the indicators are
    // calculated by ClientReport.
    public async Task<ClientReportResponse> GetClientReportAsync(
        string? startDate, string? endDate, CancellationToken cancellationToken)
    {
        var period = ReportPeriod.Parse(startDate, endDate);

        var services = _db.Services.AsNoTracking()
            .Where(s => s.ClientId == _currentUser.UserId
                        && s.Proposals.Any(p => p.Status == ProposalStatus.Accepted));
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

        var contracted = await services
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

        return ClientReport.Build(period, contracted
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
}