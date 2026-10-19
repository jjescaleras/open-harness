using OpenHarness.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenHarnessInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

app.Run();
