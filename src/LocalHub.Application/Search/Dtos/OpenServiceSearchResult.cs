using LocalHub.Application.Services.Dtos;

namespace LocalHub.Application.Search.Dtos;

public record OpenServiceSearchResult(
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

public record OpenServiceResponse(
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