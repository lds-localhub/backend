namespace LocalHub.Application.Accounts.Dtos;

// Application that starts the session (API documentation, E02). In JSON: "WEB" or "MOBILE".
public enum LoginClient
{
    Web,
    Mobile,
}