namespace LocalHub.Application.Common;

public record ListResult<T>(int Total, IReadOnlyList<T> Items);