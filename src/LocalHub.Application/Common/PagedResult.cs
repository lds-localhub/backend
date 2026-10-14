namespace LocalHub.Application.Common;

// Response of a paged list (API documentation, section 4.4): { "page", "pageSize", "total", "items" }.
public record PagedResult<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);