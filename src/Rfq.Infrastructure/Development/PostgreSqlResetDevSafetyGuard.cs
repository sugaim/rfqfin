using Npgsql;

namespace Rfq.Infrastructure;


public static class PostgreSqlResetDevSafetyGuard
{
    public const string DevelopmentEnvironment = "Development";

    public static void EnsureAllowed(string environmentName, string connectionString)
    {
        if (!string.Equals(environmentName, DevelopmentEnvironment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "reset-dev is allowed only when DOTNET_ENVIRONMENT is Development.");
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        var isLocalHost = string.Equals(connection.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.Host, "::1", StringComparison.OrdinalIgnoreCase);

        if (!isLocalHost || !string.Equals(connection.Database, "rfq", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "reset-dev may target only the local development database 'rfq'.");
        }
    }
}
