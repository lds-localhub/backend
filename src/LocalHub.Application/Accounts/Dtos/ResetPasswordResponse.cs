namespace LocalHub.Application.Accounts.Dtos;

// Response of E41. The temporary password is returned only in this response and is never stored in readable
// text (RF-F002-05, rule 5).
public record ResetPasswordResponse(string TemporaryPassword);