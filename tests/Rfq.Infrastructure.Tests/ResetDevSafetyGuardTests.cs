using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class PostgreSqlResetDevSafetyGuardTests
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5432;Database=rfq;Username=rfq;Password=rfq-dev-password";

    [Fact]
    public void AllowsOnlyDevelopmentEnvironmentWithExpectedLocalDatabase()
    {
        PostgreSqlResetDevSafetyGuard.EnsureAllowed(
            PostgreSqlResetDevSafetyGuard.DevelopmentEnvironment,
            LocalDevelopmentConnection);

        Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlResetDevSafetyGuard.EnsureAllowed("Production", LocalDevelopmentConnection));
        Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlResetDevSafetyGuard.EnsureAllowed(
                PostgreSqlResetDevSafetyGuard.DevelopmentEnvironment,
                "Host=database.example;Database=rfq;Username=rfq;Password=secret"));
        Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlResetDevSafetyGuard.EnsureAllowed(
                PostgreSqlResetDevSafetyGuard.DevelopmentEnvironment,
                "Host=localhost;Database=production;Username=rfq;Password=secret"));
    }
}
