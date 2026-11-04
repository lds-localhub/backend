namespace LocalHub.Application.Common;

// Response of a list without pages (API documentation, section 4.4): { "total", "items" }.
public record ListResult<T>(int Total, IReadOnlyList<T> Items);