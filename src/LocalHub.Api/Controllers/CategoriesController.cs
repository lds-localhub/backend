using LocalHub.Application.Categories;
using LocalHub.Application.Categories.Dtos;
using LocalHub.Application.Common;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize(Roles = "ADMIN")]
public class CategoriesController : ControllerBase
{
    private readonly CategoryService _categoryService;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(CategoryService categoryService, ICurrentUser currentUser)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
    }

    // includeInactive=true is reserved to the administrator.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ListResult<CategoryResponse>>> List(
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false)
    {
        if (includeInactive)
        {
            if (!_currentUser.IsAuthenticated)
            {
                return Challenge();
            }

            if (_currentUser.Role != UserRole.Admin)
            {
                return Forbid();
            }
        }

        return Ok(await _categoryService.ListAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, category);
    }

    [HttpPut("{serviceCategoryId:int}")]
    public async Task<ActionResult<CategoryResponse>> Rename(
        int serviceCategoryId, CategoryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.RenameAsync(serviceCategoryId, request, cancellationToken));
    }

    [HttpPost("{serviceCategoryId:int}/deactivate")]
    public async Task<ActionResult<CategoryResponse>> Deactivate(int serviceCategoryId, CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.DeactivateAsync(serviceCategoryId, cancellationToken));
    }
}