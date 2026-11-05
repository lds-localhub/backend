namespace LocalHub.Domain.Services;

/// <summary>
/// How a service receives proposals: from the providers chosen by the client or from any provider (public).
/// </summary>
public enum ProposalModel { Public, DirectRequest }