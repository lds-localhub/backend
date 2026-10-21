namespace LocalHub.Application.Search.Dtos;

// Query parameters of E18, besides page and pageSize. The reference location is given by the coordinates or by the
// address; without both, the registered location of the client is used (RF-F005-02).
public record ProviderSearchRequest(
    int? ServiceCategoryId,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? Name);