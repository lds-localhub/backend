namespace LocalHub.Domain.Services;

/// <summary>
/// Represents the state of a proposal (Requirements Specification v03, section 10.3).
/// </summary>
public enum ProposalStatus { Pending, Accepted, Declined, Void }