namespace DocumentAPI.Services.Documents;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

/// <summary>
/// Builds the resilience pipeline used by the document service to retry transient SQL Server failures
/// using an exponential backoff with jitter strategy.
/// </summary>
internal static class DocumentResiliencePipeline
{
    /// <summary>
    /// Creates a resilience pipeline that retries transient database failures with exponential backoff and jitter.
    /// </summary>
    /// <returns>The configured resilience pipeline.</returns>
    public static ResiliencePipeline Create()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(static exception => exception.IsTransient)
                    .Handle<DbUpdateException>(static exception => exception.InnerException is SqlException { IsTransient: true })
                    .Handle<TimeoutException>(),
                MaxRetryAttempts = 6,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
            })
            .Build();
    }
}
