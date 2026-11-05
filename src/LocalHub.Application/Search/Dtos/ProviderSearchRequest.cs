namespace LocalHub.Application.Search.Dtos;

public record ProviderSearchRequest(
    int? ServiceCategoryId,
    string? Address,
    string? Name);