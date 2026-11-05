namespace LocalHub.Application.Accounts.Dtos;

public record AdminSummaryResponse(int ActiveAccounts, int InactiveAccounts, int ActiveCategories);