using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class ResetDevSafetyGuardTests
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5432;Database=rfq;Username=rfq;Password=rfq-dev-password";

    [Fact]
    public void AllowsOnlyDevelopmentEnvironmentWithExpectedLocalDatabase()
    {
        ResetDevSafetyGuard.EnsureAllowed(
            ResetDevSafetyGuard.DevelopmentEnvironment,
            LocalDevelopmentConnection);

        Assert.Throws<InvalidOperationException>(() =>
            ResetDevSafetyGuard.EnsureAllowed("Production", LocalDevelopmentConnection));
        Assert.Throws<InvalidOperationException>(() =>
            ResetDevSafetyGuard.EnsureAllowed(
                ResetDevSafetyGuard.DevelopmentEnvironment,
                "Host=database.example;Database=rfq;Username=rfq;Password=secret"));
        Assert.Throws<InvalidOperationException>(() =>
            ResetDevSafetyGuard.EnsureAllowed(
                ResetDevSafetyGuard.DevelopmentEnvironment,
                "Host=localhost;Database=production;Username=rfq;Password=secret"));
    }
}
