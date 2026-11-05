using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Reviews;

public static class ReviewQueries
{
    public static IQueryable<Review> ReviewsOf(this LocalHubDbContext db, int professionalProfileId)
    {
        return db.Reviews.Where(r => r.Service.Proposals.Any(
            p => p.ProfessionalProfileId == professionalProfileId && p.Status == ProposalStatus.Accepted));
    }

    public static async Task<Dictionary<int, int>> CountReviewsAsync(
        this LocalHubDbContext db, IReadOnlyCollection<int> professionalProfileIds, CancellationToken cancellationToken)
    {
        return await db.Proposals
            .Where(p => professionalProfileIds.Contains(p.ProfessionalProfileId)
                && p.Status == ProposalStatus.Accepted
                && p.Service.Review != null)
            .GroupBy(p => p.ProfessionalProfileId)
            .Select(g => new { ProfessionalProfileId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProfessionalProfileId, x => x.Count, cancellationToken);
    }
}