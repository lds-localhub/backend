namespace LocalHub.Application.Accounts.Dtos;

// Response of E39: the counters of the administrator's home page (W50). The administrator accounts are not counted,
// as in E05.
public record AdminSummaryResponse(int ActiveAccounts, int InactiveAccounts, int ActiveCategories);