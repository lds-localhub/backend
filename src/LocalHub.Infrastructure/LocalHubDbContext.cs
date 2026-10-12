using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

public class LocalHubDbContext(DbContextOptions<LocalHubDbContext> options) : DbContext(options)
{
}