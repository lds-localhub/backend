using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Profiles;
using LocalHub.Application.Profiles.Dtos;
using LocalHub.Application.Reviews;
using LocalHub.Application.Reviews.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F004 (E12 to E15) and the service radius of F015 (E17). The administrator has no profile and gets 403.
[ApiController]
[Route("api/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly ReviewService _reviewService;

    public ProfilesController(ProfileService profileService, ReviewService reviewService)
    {
        _profileService = profileService;
        _reviewService = reviewService;
    }

    // E15: the same access as E13 (a client sees any active provider; a provider only its own reviews).
    [HttpGet("{professionalProfileId:int}/reviews")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PagedResult<ReviewListItem>>> ListReviews(
        int professionalProfileId,
        CancellationToken cancellationToken,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _reviewService.ListByProfileAsync(
            professionalProfileId, new PageRequest(page, pageSize), cancellationToken));
    }

    // E12: only a provider without a profile (a second profile gets 409).
    [HttpPost]
    [Authorize(Roles = "PROVIDER")]
    public async Task<ActionResult<ProfileResponse>> Create(
        CreateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profileService.CreateAsync(request, cancellationToken);
        return Created($"/api/profiles/{profile.ProfessionalProfileId}", profile);
    }

    // E13: a client sees any active provider; a provider only sees its own profile.
    [HttpGet("{professionalProfileId:int}")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProfileResponse>> Get(int professionalProfileId, CancellationToken cancellationToken)
    {
        return Ok(await _profileService.GetAsync(professionalProfileId, cancellationToken));
    }

    // E14
    [HttpPut("me")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProfileResponse>> UpdateMine(
        UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _profileService.UpdateMineAsync(request, cancellationToken));
    }

    // E17
    [HttpPut("me/service-radius")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ServiceRadiusResponse>> SetMyServiceRadius(
        ServiceRadiusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _profileService.SetMyServiceRadiusAsync(request, cancellationToken));
    }
}