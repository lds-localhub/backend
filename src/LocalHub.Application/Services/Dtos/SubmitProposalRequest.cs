using System.Text.Json.Serialization;

namespace LocalHub.Application.Services.Dtos;

// Body of E25. Fields that are not listed here are rejected with 400.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SubmitProposalRequest(decimal? Amount, string? Conditions, string? Obs);