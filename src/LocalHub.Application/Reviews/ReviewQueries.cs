using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Reviews;

public static class ReviewQueries
{
    // The reviews received by a provider: those of the services in which its proposal was accepted (RF-F012-01,
    // rule 6). Used in the average rating (E32), in reviewCount (E12 to E14) and in the list of E15.
    public static IQueryable<Review> ReviewsOf(this LocalHubDbContext db, int professionalProfileId)
    {
        return db.Reviews.Where(r => r.Service.Proposals.Any(
            p => p.ProfessionalProfileId == professionalProfileId && p.Status == ProposalStatus.Accepted));
    }

    // The reviewCount of several providers at once (E22): the same reviews as ReviewsOf, counted by profile. A profile
    // without reviews is not in the dictionary.
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