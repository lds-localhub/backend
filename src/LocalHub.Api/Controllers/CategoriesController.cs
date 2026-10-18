using LocalHub.Application.Categories;
using LocalHub.Application.Categories.Dtos;
using LocalHub.Application.Common;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F003: the management of categories is reserved to the administrator; other roles get 403 (RF-F003-01 to RF-F003-03).
// The list of active categories is public (RF-F003-04; RF-F001-03, rule 3).
[ApiController]
[Route("api/categories")]
[Authorize(Roles = "ADMIN")]
public class CategoriesController(CategoryService categoryService, ICurrentUser currentUser) : ControllerBase
{
    // E08: without includeInactive, anyone gets the active categories. With includeInactive=true, a request
    // without a session gets 401 and a request of another role gets 403 (API documentation, E08).
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false)
    {
        if (includeInactive)
        {
            if (!currentUser.IsAuthenticated)
            {
                return Challenge();
            }

            if (currentUser.Role != UserRole.Admin)
            {
                return Forbid();
            }
        }

        return Ok(await categoryService.ListAsync(includeInactive, cancellationToken));
    }

    // E09
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);
        return Created($"/api/categories/{category.ServiceCategoryId}", category);
    }

    // E10
    [HttpPut("{serviceCategoryId:int}")]
    public async Task<ActionResult<CategoryResponse>> Rename(
        int serviceCategoryId, CategoryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await categoryService.RenameAsync(serviceCategoryId, request, cancellationToken));
    }

    // E11
    [HttpPost("{serviceCategoryId:int}/deactivate")]
    public async Task<ActionResult<CategoryResponse>> Deactivate(int serviceCategoryId, CancellationToken cancellationToken)
    {
        return Ok(await categoryService.DeactivateAsync(serviceCategoryId, cancellationToken));
    }
}