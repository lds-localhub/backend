using LocalHub.Application.Services.Dtos;

namespace LocalHub.Application.Search.Dtos;

// One result of E19 (RF-F006-01): the published job without any data of the Client. distanceKm is calculated from
// the location of the provider and has one decimal place; withinServiceRadius tells whether the job is inside the
// service radius of the profile.
public record PublishedJobSearchResult(
    int ServiceId,
    ServiceCategorySummary ServiceCategory,
    string Title,
    string Description,
    string Location,
    decimal DistanceKm,
    bool WithinServiceRadius,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    DateTimeOffset CreationDate);

// E38 (RF-F011-01): the detail of a published job, with the proposal that the provider already presented, if any
// (consulted in E37). It has no data of the Client nor the proposals of other providers.
public record PublishedJobResponse(
    int ServiceId,
    ServiceCategorySummary ServiceCategory,
    string Title,
    string Description,
    string Location,
    decimal? DistanceKm,
    bool WithinServiceRadius,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    DateTimeOffset CreationDate,
    int? MyProposalId);