using System.Text.Json.Serialization;

using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Body of E20, with the two variants of the API documentation: DIRECT_REQUEST requires professionalProfileIds and
// PUBLIC does not accept it. Without location, the client's registered location is used. Fields that are not listed
// here are rejected with 400 (unevaluatedProperties: false in the schema of E20).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateServiceRequest(
    ProposalModel? ProposalModel,
    int? ServiceCategoryId,
    string? Description,
    DateOnly? RequestedStartDate,
    DateOnly? RequestedEndDate,
    string? Location,
    IReadOnlyList<int>? ProfessionalProfileIds);