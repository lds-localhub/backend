using LocalHub.Application.Categories.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Categories;

public class CategoryService
{
    private const string DuplicateNameMessage = "Já existe uma categoria com este nome.";

    private readonly LocalHubDbContext _db;

    public CategoryService(LocalHubDbContext db)
    {
        _db = db;
    }

    public async Task<ListResult<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = _db.ServiceCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.Status == ActivationStatus.Active);
        }

        var items = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.ServiceCategoryId)
            .Select(c => new CategoryResponse(c.ServiceCategoryId, c.Name, c.Status))
            .ToListAsync(cancellationToken);
        return new ListResult<CategoryResponse>(items.Count, items);
    }

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = ServiceCategory.Create(request.Name!);
        await EnsureNameIsFreeAsync(category.Name, exceptCategoryId: null, cancellationToken);

        _db.ServiceCategories.Add(category);
        await SaveAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    public async Task<CategoryResponse> RenameAsync(
        int serviceCategoryId, CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await FindAsync(serviceCategoryId, cancellationToken);

        category.Rename(request.Name!);
        await EnsureNameIsFreeAsync(category.Name, serviceCategoryId, cancellationToken);
        await SaveAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    public async Task<CategoryResponse> DeactivateAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        var category = await FindAsync(serviceCategoryId, cancellationToken);

        // An inactive category is "already inactive", whatever its use.
        if (category.Status == ActivationStatus.Active)
        {
            var usage = await CountUsageAsync(serviceCategoryId, cancellationToken);
            if (usage.IsInUse)
            {
                throw new CategoryInUseException(DescribeUsage(usage));
            }
        }

        category.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    public static string DescribeUsage(CategoryUsageCount usage)
    {
        var parts = new List<string>();
        if (usage.ActiveProviders > 0)
        {
            parts.Add(usage.ActiveProviders == 1
                ? "a 1 Prestador ativo"
                : $"a {usage.ActiveProviders} Prestadores ativos");
        }

        if (usage.ServicesInProgress > 0)
        {
            parts.Add(usage.ServicesInProgress == 1
                ? "a 1 serviço em curso"
                : $"a {usage.ServicesInProgress} serviços em curso");
        }

        return $"A categoria está associada {string.Join(" e ", parts)}.";
    }

    private async Task<CategoryUsageCount> CountUsageAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        int activeProviders = await _db.ProfessionalProfiles
            .Where(p => p.ServiceProvider.Status == ActivationStatus.Active
                        && p.ServiceAreas.Any(a => a.ServiceCategoryId == serviceCategoryId))
            .CountAsync(cancellationToken);

        int servicesInProgress = await _db.Services
            .Where(s => s.ServiceCategoryId == serviceCategoryId
                        && (s.Status == ServiceStatus.Open || s.Status == ServiceStatus.Assigned))
            .CountAsync(cancellationToken);

        return new CategoryUsageCount(activeProviders, servicesInProgress);
    }

    private async Task<ServiceCategory> FindAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        var category = await _db.ServiceCategories
            .SingleOrDefaultAsync(c => c.ServiceCategoryId == serviceCategoryId, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException("Categoria não encontrada.");
        }

        return category;
    }

    // Case-insensitive comparison done in the database: ToLower() is translated to lower().
    private async Task EnsureNameIsFreeAsync(string name, int? exceptCategoryId, CancellationToken cancellationToken)
    {
        string lowerName = name.ToLowerInvariant();
        // EF translates ToLower() to SQL; the culture-aware overloads cannot be translated.
#pragma warning disable CA1304, CA1311, CA1862
        bool exists = await _db.ServiceCategories.AnyAsync(
            c => c.Name.ToLower() == lowerName && c.ServiceCategoryId != exceptCategoryId,
            cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862

        if (exists)
        {
            throw new DomainException(DomainErrorKind.Conflict, DuplicateNameMessage, "name");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two requests with the same name at the same time: the unique index rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && postgres.ConstraintName == "UX_ServiceCategory_name")
            {
                throw new DomainException(DomainErrorKind.Conflict, DuplicateNameMessage, "name");
            }

            throw;
        }
    }
}