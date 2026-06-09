namespace DocumentAPI.Services.Health.Contracts;

/// <summary>
/// Represents the evaluated health state of the service.
/// </summary>
/// <param name="Status">The health status value.</param>
/// <param name="IsAvailable">A value indicating whether the service should respond with an available HTTP status code.</param>
/// <param name="Checks">The per-dependency health checks associated with the evaluated state.</param>
// <lab id="00-health-contract" state="starter">
//
// TODO: Implement the health state result contract, making the checks dictionary nullable to allow for incremental implementation in the health evaluation logic.
//
// </lab>
// <lab id="00-health-contract" state="solution">
public sealed record HealthStateResult(
	HealthStatus Status,
	bool IsAvailable,
	IReadOnlyDictionary<string, HealthDependencyState> Checks);
// </lab>
