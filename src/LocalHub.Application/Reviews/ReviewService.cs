using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Reviews.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Reviews;

// The review of a completed service, the recalculation of the average rating and the reviews of a provider (F012).
public class ReviewService
{
    private const string AlreadyReviewedMessage = "Este serviço já foi avaliado.";

    private readonly LocalHubDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReviewService(LocalHubDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // E32 (RF-F012-01, RF-F012-02). The fields were validated by the DTO (rule R17); the actor, the state and the
    // single review are decided by Service.AddReview. The review and the new average rating of the provider are saved
    // with a single SaveChangesAsync (rule R1).
    public async Task<ReviewResponse> CreateAsync(
        int serviceId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var service = await _db.Services
            .Include(s => s.Review)
            .Include(s => s.Proposals.Where(p => p.Status == ProposalStatus.Accepted))
            .ThenInclude(p => p.ProfessionalProfile)
            .SingleOrDefaultAsync(s => s.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new NotFoundException("Serviço não encontrado.");
        }

        var review = service.AddReview(_currentUser.UserId, request.Rating!.Value, request.Comment, _timeProvider.GetUtcNow());

        // RF-F012-01, rule 6: the review is of the provider whose proposal was accepted.
        var provider = service.Proposals.Single().ProfessionalProfile;
        var ratings = await _db.ReviewsOf(provider.ProfessionalProfileId)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);
        ratings.Add(review.Rating);
        provider.RecalculateAverageRating(ratings);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two reviews of the same service at the same time: the UNIQUE of serviceId rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainException(DomainErrorKind.Conflict, AlreadyReviewedMessage,
                    errorType: Review.AlreadyExistsErrorType);
            }

            // Any other error of the database is not expected: the request gets 500.
            throw;
        }

        return ReviewResponse.From(review);
    }

    // E15 (RF-F004-03, RF-F012-01): the reviews of a provider, newest first. The access is the same as in E13: a client
    // sees the reviews of an active provider; a provider only sees its own reviews. A provider without a profile is
    // stopped before, by the filter (403 perfil-em-falta).
    public async Task<PagedResult<ReviewListItem>> ListByProfileAsync(
        int professionalProfileId, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var profile = await _db.ProfessionalProfiles.AsNoTracking()
            .Where(p => p.ProfessionalProfileId == professionalProfileId)
            .Select(p => new { p.ServiceProviderId, p.ServiceProvider.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (_currentUser.Role == UserRole.Provider)
        {
            if (profile == null)
            {
                throw new NotFoundException("Perfil não encontrado.");
            }

            if (profile.ServiceProviderId != _currentUser.UserId)
            {
                throw new ForbiddenException("Só pode consultar as avaliações do seu próprio perfil.");
            }
        }

        // The profile of a provider with a deactivated account is not shown to the client (RF-F004-03, rule 2).
        else if (profile == null || profile.Status != ActivationStatus.Active)
        {
            throw new NotFoundException("Perfil não encontrado.");
        }

        var reviews = _db.ReviewsOf(professionalProfileId).AsNoTracking();
        int total = await reviews.CountAsync(cancellationToken);
        var items = await reviews
            .OrderByDescending(r => r.CreationDate)
            .ThenByDescending(r => r.ReviewId)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(r => new ReviewListItem(r.ReviewId, r.Rating, r.Comment, r.Service.Client.Name, r.CreationDate))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewListItem>(page.Page, page.PageSize, total, items);
    }
}