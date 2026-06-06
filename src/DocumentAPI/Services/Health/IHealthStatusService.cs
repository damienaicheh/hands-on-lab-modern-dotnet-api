using DocumentAPI.Services.Health.Contracts;

namespace DocumentAPI.Services.Health;

/// <summary>
/// Defines the contract used to evaluate the current service health state.
/// </summary>
public interface IHealthStatusService
{
    /// <summary>
    /// Calculates the current health state of the service and its dependencies.
    /// </summary>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The evaluated health state.</returns>
    Task<HealthStateResult> GetStatusAsync(CancellationToken cancellationToken);
}
