namespace DocumentAPI.Services.Health.Contracts;

/// <summary>
/// Represents the evaluated health state of the service.
/// </summary>
/// <param name="Status">The health status label.</param>
/// <param name="IsAvailable">A value indicating whether the service should respond with an available HTTP status code.</param>
/// <param name="Checks">The per-dependency health checks associated with the evaluated state.</param>
public sealed record HealthStateResult(
	string Status,
	bool IsAvailable,
	IReadOnlyDictionary<string, HealthDependencyState> Checks);
