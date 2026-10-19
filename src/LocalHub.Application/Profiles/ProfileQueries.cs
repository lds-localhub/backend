using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Profiles;

public static class ProfileQueries
{
    // The professionalProfileId of E02, E04 and E16: null for clients, administrators and providers without a profile.
    public static Task<int?> FindProfileIdAsync(this LocalHubDbContext db, int userId, CancellationToken cancellationToken)
    {
        return db.ProfessionalProfiles
            .Where(p => p.ServiceProviderId == userId)
            .Select(p => (int?)p.ProfessionalProfileId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}