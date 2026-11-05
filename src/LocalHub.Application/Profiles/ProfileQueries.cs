using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Profiles;

public static class ProfileQueries
{
    public static Task<int?> FindProfileIdAsync(this LocalHubDbContext db, int userId, CancellationToken cancellationToken)
    {
        return db.ProfessionalProfiles
            .Where(p => p.ServiceProviderId == userId)
            .Select(p => (int?)p.ProfessionalProfileId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}