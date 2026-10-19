namespace LocalHub.Domain.Services;

/// <summary>
/// Represents the state of a direct request (Requirements Specification v03, section 10.4).
/// </summary>
public enum DirectRequestStatus { Pending, ProposalSubmitted, Declined, Void }