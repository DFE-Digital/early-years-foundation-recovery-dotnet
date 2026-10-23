using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public sealed class ApplicationDatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("The application database is reachable.")
            : HealthCheckResult.Unhealthy("The application database is not reachable.");
    }
}
