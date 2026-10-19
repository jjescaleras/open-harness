using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace OpenHarness.Infrastructure.Persistence;

public sealed class OpenHarnessDbContextFactory : IDesignTimeDbContextFactory<OpenHarnessDbContext>
{
    public OpenHarnessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("OPENHARNESS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=open_harness;Username=harness;Password=harness_dev_password";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<OpenHarnessDbContext>();
        optionsBuilder
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.UseVector();
                npgsql.EnableRetryOnFailure(5);
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "harness");
            })
            .UseSnakeCaseNamingConvention();

        return new OpenHarnessDbContext(optionsBuilder.Options);
    }
}
