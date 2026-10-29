using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

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
}
