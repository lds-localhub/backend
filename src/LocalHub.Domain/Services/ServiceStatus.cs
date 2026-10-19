namespace LocalHub.Domain.Services;

/// <summary>
/// Represents the state of a service (Requirements Specification v03, sections 10.1 and 10.2).
/// </summary>
public enum ServiceStatus { Open, Assigned, Completed, Cancelled }