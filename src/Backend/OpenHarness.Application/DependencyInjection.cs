using Microsoft.Extensions.DependencyInjection;
using OpenHarness.Application.Features.Auth;
using OpenHarness.Application.Features.Sessions;
using OpenHarness.Application.Features.Settings;

namespace OpenHarness.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenHarnessApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddScoped<ISettingsService, SettingsService>();
        return services;
    }
}

