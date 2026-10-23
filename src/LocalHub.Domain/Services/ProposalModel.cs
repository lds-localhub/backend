namespace LocalHub.Domain.Services;

/// <summary>
/// Represents how a service receives proposals: from the providers chosen by the client (direct requests, F009) or
/// from any provider that finds it in the search of published jobs (F010). Same values as the type proposal_model.
/// </summary>
public enum ProposalModel { Public, DirectRequest }