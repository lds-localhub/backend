using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Services;

// Creation and life cycle of the services, direct requests and proposals (F007 to F011).
public class ServiceWorkflowService(
    LocalHubDbContext db,
    LocationResolver locationResolver,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    // E20 (RF-F009-01, RF-F009-03, RF-F010-01). The checks go from the cheapest to the most expensive: first the
    // fields, then the category and the chosen profiles (database), and last the address (Nominatim). Nothing is
    // changed before the address is converted (rule R2), and the service and its direct requests are saved with a
    // single SaveChangesAsync, in one transaction (rule R1): on any error nothing is created.
    public async Task<ServiceResponse> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var client = await db.Users.SingleOrDefaultAsync(u => u.UserId == currentUser.UserId, cancellationToken)
                     ?? throw new NotFoundException("Utilizador não encontrado.");
        // PostgreSQL keeps microseconds (timestamptz), so the instant is truncated before it is used: the creationDate
        // returned now is the same that will be read from the database later (E21, E22).
        var utcNow = timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        var profileIds = request.ProfessionalProfileIds?.Distinct().ToList() ?? [];

        var errors = new FieldErrors();
        CollectFieldErrors(errors, request, now);
        errors.ThrowIfAny();

        var category = await db.ServiceCategories
            .SingleOrDefaultAsync(c => c.ServiceCategoryId == request.ServiceCategoryId, cancellationToken);
        if (category is null)
        {
            errors.Add(Service.CategoryField, "A categoria indicada não existe.");
        }
        else
        {
            errors.Collect(() => Service.ValidateCategory(category), Service.CategoryField);
        }

        var profiles = await LoadProfilesAsync(profileIds, cancellationToken);
        var recipientErrors = profileIds
            .Where(id => profiles.All(p => p.ProfessionalProfileId != id))
            .Select(id => $"O perfil {id} não existe.")
            .ToList();
        recipientErrors.AddRange(Service.DescribeIneligible(profiles, category?.ServiceCategoryId));

        if (recipientErrors.Count > 0)
        {
            errors.Add(Service.RecipientsField, [.. recipientErrors]);
        }

        bool hasAddress = !string.IsNullOrWhiteSpace(request.Location);
        if (!hasAddress && (client.Address is null || client.Latitude is null || client.Longitude is null))
        {
            errors.Add(Service.LocationField,
                "Indique o local de realização: ainda não tem uma localização registada.");
        }

        errors.ThrowIfAny();

        // Without location, the client's registered location; otherwise the address converted into coordinates,
        // without changing the registered location (RF-F009-01, rule 3; RF-F010-01, rule 6).
        var location = hasAddress
            ? await locationResolver.ResolveAddressAsync(request.Location!, Service.LocationField, cancellationToken)
            : new ResolvedLocation(client.Address!, GeoPoint.Create(client.Latitude!.Value, client.Longitude!.Value));

        var service = Service.Create(
            client,
            category!,
            request.ProposalModel!.Value,
            request.Description,
            request.RequestedStartDate,
            request.RequestedEndDate,
            location.Address,
            location.Coordinates,
            profiles,
            now);

        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResponse.From(service);
    }

    // The rules that need neither the database nor the Nominatim, all reported in the same response.
    private static void CollectFieldErrors(FieldErrors errors, CreateServiceRequest request, DateTimeOffset now)
    {
        errors.Collect(() => Service.ValidateProposalModel(request.ProposalModel), Service.ProposalModelField);
        if (request.ServiceCategoryId is null)
        {
            errors.Add(Service.CategoryField, "Indique a categoria do serviço.");
        }

        errors.Collect(() => Service.ValidateDescription(request.Description), Service.DescriptionField);
        errors.Collect(
            () => Service.ValidateRequestedDates(
                request.RequestedStartDate, request.RequestedEndDate, Service.TodayInPortugal(now)),
            Service.StartDateField);
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            errors.Collect(() => Service.ValidateLocation(request.Location), Service.LocationField);
        }

        if (errors.Contains(Service.ProposalModelField))
        {
            return;
        }

        // PUBLIC does not accept the field at all, not even an empty list (API documentation, E20).
        if (request.ProposalModel == ProposalModel.Public && request.ProfessionalProfileIds is not null)
        {
            errors.Add(Service.RecipientsField,
                "Um trabalho publicado não tem destinatários: não indique professionalProfileIds.");
            return;
        }

        errors.Collect(
            () => Service.ValidateRecipientCount(request.ProposalModel!.Value, request.ProfessionalProfileIds?.Count ?? 0),
            Service.RecipientsField);
    }

    // With the provider (account state) and the categories of the profile (RF-F009-01, rules 5 and 6).
    private async Task<List<ProfessionalProfile>> LoadProfilesAsync(
        List<int> profileIds, CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return [];
        }

        return await db.ProfessionalProfiles
            .Include(p => p.ServiceProvider)
            .Include(p => p.ServiceAreas)
            .Where(p => profileIds.Contains(p.ProfessionalProfileId))
            .ToListAsync(cancellationToken);
    }
}