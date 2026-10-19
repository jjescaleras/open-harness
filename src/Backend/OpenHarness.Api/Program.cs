using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenHarness.Api.Endpoints;
using OpenHarness.Application;
using OpenHarness.Domain.Common;
using OpenHarness.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Layer Registrations
builder.Services.AddOpenHarnessApplication();
builder.Services.AddOpenHarnessInfrastructure(builder.Configuration);

// JWT Bearer Configuration with MapInboundClaims = false (Zero-Allocation & Standard Claims)
var jwtConfig = builder.Configuration.GetSection("Jwt");
var secretKey = jwtConfig["SecretKey"]
    ?? jwtConfig["Key"]
    ?? "OpenHarnessSuperSecretDevelopmentKey1234567890!@#$%^&*()_+";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtConfig["Issuer"] ?? "OpenHarness",
            ValidateAudience = true,
            ValidAudience = jwtConfig["Audience"] ?? "OpenHarnessClient",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SettingsEndpoints.AdminOnlyPolicy, policy => policy.RequireRole(Roles.Admin));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
app.MapAuthEndpoints();
app.MapSessionEndpoints();
app.MapSettingsEndpoints();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }
