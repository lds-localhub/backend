using LocalHub.Application.Categories.Dtos;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Categories;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Categories;

public class CategoryService
{
    private const string DuplicateNameMessage = "Já existe uma categoria com este nome.";

    private readonly LocalHubDbContext _db;
    private readonly ICategoryUsage _categoryUsage;

    public CategoryService(LocalHubDbContext db, ICategoryUsage categoryUsage)
    {
        _db = db;
        _categoryUsage = categoryUsage;
    }

    // E08: only the active categories, unless the administrator asks for all of them (RF-F003-04).
    // The controller checks that includeInactive is only used by the administrator.
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = _db.ServiceCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.Status == ActivationStatus.Active);
        }

        return await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.ServiceCategoryId)
            .Select(c => new CategoryResponse(c.ServiceCategoryId, c.Name, c.Status))
            .ToListAsync(cancellationToken);
    }

    // E09: the new category is active and its name is unique, ignoring upper and lower case (RF-F003-01).
    // The name was validated by the DTO (rule R17).
    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = ServiceCategory.Create(request.Name!);
        await EnsureNameIsFreeAsync(category.Name, exceptCategoryId: null, cancellationToken);

        _db.ServiceCategories.Add(category);
        await SaveAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    // E10: only the name changes; the associations to profiles and services are kept (RF-F003-02).
    public async Task<CategoryResponse> RenameAsync(
        int serviceCategoryId, CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await FindAsync(serviceCategoryId, cancellationToken);

        category.Rename(request.Name!);
        await EnsureNameIsFreeAsync(category.Name, serviceCategoryId, cancellationToken);
        await SaveAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    // E11: the category becomes INACTIVE, unless it is used by active providers or services in progress (RF-F003-03).
    public async Task<CategoryResponse> DeactivateAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        var category = await FindAsync(serviceCategoryId, cancellationToken);

        // An inactive category gets the "already inactive" error, whatever its use.
        if (category.Status == ActivationStatus.Active)
        {
            var usage = await _categoryUsage.CountAsync(serviceCategoryId, cancellationToken);
            if (usage.IsInUse)
            {
                throw new CategoryInUseException(DescribeUsage(usage));
            }
        }

        category.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    // For example: "A categoria está associada a 3 Prestadores ativos e a 1 serviço em curso." (API documentation, E11).
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

    private async Task<ServiceCategory> FindAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        var category = await _db.ServiceCategories
            .SingleOrDefaultAsync(c => c.ServiceCategoryId == serviceCategoryId, cancellationToken);
        if (category is null)
        {
            throw new NotFoundException("Categoria não encontrada.");
        }

        return category;
    }

    // The names keep their upper case letters ("Canalização"), so the comparison ignoring upper and lower case is done
    // here, in the database: ToLower() is translated to lower(). The index UX_ServiceCategory_name only rejects equal
    // names, when two requests arrive at the same time.
    private async Task EnsureNameIsFreeAsync(string name, int? exceptCategoryId, CancellationToken cancellationToken)
    {
        string lowerName = name.ToLowerInvariant();
        // The analyzers ask for a culture or for string.Equals(..., StringComparison), but this expression is not run
        // in C#: Entity Framework translates it to SQL, and only ToLower() can be translated (ToLowerInvariant and
        // string.Equals with a StringComparison cannot).
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

            // Any other error of the database is not expected: the request gets 500.
            throw;
        }
    }
}
