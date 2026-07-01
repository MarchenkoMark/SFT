using System.Reflection;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SnakeForTwo.Api;
using SnakeForTwo.Game.Application;
using SnakeForTwo.Infrastructure;
using SnakeForTwo.Infrastructure.Persistence;

const string ServiceName = "SnakeForTwo.Api";
const string ServiceNamespace = "snakefortwo";

var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly;
var serviceVersion = assembly.GetName().Version?.ToString() ?? "unknown";
var informationalVersion = assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? serviceVersion;
var deploymentEnvironment = builder.Environment.EnvironmentName.ToLowerInvariant();
var otlpEndpointConfigured = !string.IsNullOrWhiteSpace(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
var persistenceOptions = builder.Configuration
    .GetSection(PersistenceOptions.SectionName)
    .Get<PersistenceOptions>() ?? new PersistenceOptions();
var persistenceConnectionString = builder.Configuration.GetConnectionString("SnakeForTwo");
var persistenceEnabled = persistenceOptions.Enabled ||
    !string.IsNullOrWhiteSpace(persistenceConnectionString);

if (persistenceEnabled && string.IsNullOrWhiteSpace(persistenceConnectionString))
{
    throw new InvalidOperationException(
        "Persistence is enabled but ConnectionStrings:SnakeForTwo is not configured.");
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: ServiceName,
            serviceNamespace: ServiceNamespace,
            serviceVersion: informationalVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = deploymentEnvironment
        }))
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(GameMetrics.MeterName);

        if (otlpEndpointConfigured)
        {
            metrics.AddOtlpExporter();
        }
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();

        if (otlpEndpointConfigured)
        {
            tracing.AddOtlpExporter();
        }
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.ParseStateValues = true;
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(
            serviceName: ServiceName,
            serviceNamespace: ServiceNamespace,
            serviceVersion: informationalVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = deploymentEnvironment
        }));

    if (otlpEndpointConfigured)
    {
        logging.AddOtlpExporter();
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddSingleton<IRoomLifecycleLogger, RoomLifecycleLogger>();
builder.Services.Configure<GameRuntimeOptions>(
    builder.Configuration.GetSection(GameRuntimeOptions.SectionName));
builder.Services.Configure<WebSocketRateLimitOptions>(
    builder.Configuration.GetSection(WebSocketRateLimitOptions.SectionName));
builder.Services.Configure<PersistenceOptions>(
    builder.Configuration.GetSection(PersistenceOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRoomIdentityProvider, SecureRoomIdentityProvider>();
if (persistenceEnabled)
{
    builder.Services.AddDbContext<SnakeForTwoDbContext>(options =>
        options.UseNpgsql(persistenceConnectionString));
    builder.Services.AddScoped<PostgresMatchSummaryStore>();
    builder.Services.AddScoped<IMatchSummaryWriter>(serviceProvider =>
        serviceProvider.GetRequiredService<PostgresMatchSummaryStore>());
    builder.Services.AddScoped<IMatchSummaryQuery>(serviceProvider =>
        serviceProvider.GetRequiredService<PostgresMatchSummaryStore>());
    builder.Services.AddScoped<ILeaderboardQuery>(serviceProvider =>
        serviceProvider.GetRequiredService<PostgresMatchSummaryStore>());
    builder.Services.AddSingleton<BackgroundGameEventPublisher>();
    builder.Services.AddSingleton<IGameEventPublisher>(serviceProvider =>
        serviceProvider.GetRequiredService<BackgroundGameEventPublisher>());
    builder.Services.AddHostedService<GameEventPersistenceHostedService>();
}
else
{
    builder.Services.AddSingleton<IGameEventPublisher, InMemoryGameEventPublisher>();
}
builder.Services.AddSingleton<IGameMetrics, GameMetrics>();
builder.Services.AddSingleton<IRoomCoordinator>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<GameRuntimeOptions>>()
        .Value;
    var identityProvider = serviceProvider.GetRequiredService<IRoomIdentityProvider>();
    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
    var lifecycleLogger = serviceProvider.GetRequiredService<IRoomLifecycleLogger>();
    var metrics = serviceProvider.GetRequiredService<IGameMetrics>();

    return new GameRoomCoordinator(options, identityProvider, timeProvider, lifecycleLogger, metrics);
});
builder.Services.AddSingleton<WebSocketConnectionRegistry>();
builder.Services.AddSingleton<WebSocketEndpoint>();
builder.Services.AddHostedService<RoomTimerHostedService>();

var app = builder.Build();

if (persistenceEnabled && persistenceOptions.ApplyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<SnakeForTwoDbContext>()
        .Database
        .MigrateAsync();
}

app.UseCors("Frontend");
app.UseWebSockets();

app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation(
        "Server started: {Service} {Version} in {Environment}.",
        ServiceName,
        informationalVersion,
        app.Environment.EnvironmentName));

app.MapGet("/", () => Results.Ok(new
{
    service = "SnakeForTwo.Api",
    status = "ready",
    websocket = "/ws"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "SnakeForTwo.Api",
    time = TimeProvider.System.GetUtcNow()
}));

app.MapGet("/version", () => Results.Ok(new
{
    service = "SnakeForTwo.Api",
    version = serviceVersion,
    informationalVersion
}));

app.MapGet("/ping", () => Results.Ok(new
{
    message = "pong",
    time = TimeProvider.System.GetUtcNow()
}));

app.MapPersistenceEndpoints(persistenceEnabled);

app.Map("/ws", async context =>
    await context.RequestServices
        .GetRequiredService<WebSocketEndpoint>()
        .HandleAsync(context));

app.Run();
