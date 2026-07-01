using System.Reflection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using SnakeForTwo.Api;
using SnakeForTwo.Game.Application;
using SnakeForTwo.Infrastructure;

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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRoomIdentityProvider, SecureRoomIdentityProvider>();
builder.Services.AddSingleton<IGameEventPublisher, InMemoryGameEventPublisher>();
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

app.Map("/ws", async context =>
    await context.RequestServices
        .GetRequiredService<WebSocketEndpoint>()
        .HandleAsync(context));

app.Run();
