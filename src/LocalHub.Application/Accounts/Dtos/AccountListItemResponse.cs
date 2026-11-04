using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

// Account in the list of E05, with its conduct alerts (RF-F002-01). E06 and E07 return AccountResponse, without alerts.
public record AccountListItemResponse(
    int UserId,
    string Name,
    string Email,
    UserRole Role,
    ActivationStatus Status,
    IReadOnlyList<AccountAlertResponse> Alerts);