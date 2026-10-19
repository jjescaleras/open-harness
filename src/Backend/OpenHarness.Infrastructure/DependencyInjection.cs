using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Infrastructure.Agents.Adapters;
using OpenHarness.Infrastructure.Agents.Pi;
using OpenHarness.Infrastructure.Persistence;
using OpenHarness.Infrastructure.Persistence.Interceptors;
using OpenHarness.Infrastructure.Security;

namespace OpenHarness.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenHarnessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OpenHarnessDb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=open_harness;Username=harness;Password=harness_dev_password";

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();
        services.TryAddSingleton<IJwtTokenProvider, JwtTokenProvider>();
        services.TryAddSingleton<IApprovalGate, InMemoryApprovalGate>();

        var piRpcSection = configuration.GetSection(PiRpcOptions.SectionName);
        var piOptions = new PiRpcOptions
        {
            ExecutablePath = piRpcSection["ExecutablePath"] ?? "pi",
            DefaultWorkingDirectory = piRpcSection["DefaultWorkingDirectory"],
            StartupTimeoutSeconds = int.TryParse(piRpcSection["StartupTimeoutSeconds"], out var s) ? s : 5
        };
        services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(piOptions));
        services.TryAddSingleton<IPiRpcSessionFactory, PiRpcSessionFactory>();
        services.TryAddTransient<IChatClient, PiChatClientAdapter>();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);

        services.AddDbContextPool<OpenHarnessDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(dataSource, npgsql =>
                {
                    npgsql.UseVector();
                    npgsql.EnableRetryOnFailure(5);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "harness");
                })
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IOpenHarnessDbContext>(sp => sp.GetRequiredService<OpenHarnessDbContext>());

        return services;
    }
}
