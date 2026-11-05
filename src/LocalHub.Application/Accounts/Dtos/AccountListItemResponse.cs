using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

public record AccountListItemResponse(
    int UserId,
    string Name,
    string Email,
    UserRole Role,
    ActivationStatus Status,
    IReadOnlyList<AccountAlertResponse> Alerts);