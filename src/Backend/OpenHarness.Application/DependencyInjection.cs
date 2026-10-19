using Microsoft.Extensions.DependencyInjection;
using OpenHarness.Application.Features.Auth;

namespace OpenHarness.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenHarnessApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

