using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DoradoCloud.Modules.Data;

/// <summary>
/// Design-time factory for <see cref="DoradoDbContext"/>. The application host
/// defaults to SQLite when no PostgreSQL connection string is configured, which
/// previously caused <c>dotnet ef migrations add</c> to scaffold SQLite-typed
/// migrations against a PostgreSQL snapshot. This factory always targets the
/// Npgsql provider so migrations match production.
/// </summary>
public sealed class DoradoDbContextFactory : IDesignTimeDbContextFactory<DoradoDbContext>
{
    private const string FallbackConnection =
        "Host=localhost;Database=doradocloud;Username=dorado;Password=doradocloud";

    public DoradoDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = FallbackConnection;
        }

        var options = new DbContextOptionsBuilder<DoradoDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new DoradoDbContext(options);
    }
}
