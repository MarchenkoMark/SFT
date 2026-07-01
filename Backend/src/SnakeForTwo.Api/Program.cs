using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
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
var authOptions = builder.Configuration
    .GetSection(AuthOptions.SectionName)
    .Get<AuthOptions>() ?? new AuthOptions();
var googleAuthConfigured =
    !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"]);

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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
if (!string.IsNullOrWhiteSpace(authOptions.DataProtectionKeysPath))
{
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(authOptions.DataProtectionKeysPath))
        .SetApplicationName("SnakeForTwo.Api");
}

var authentication = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = authOptions.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.ClaimsIssuer = ServiceName;
    });

if (googleAuthConfigured && persistenceEnabled)
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.SaveTokens = false;
        options.Events.OnCreatingTicket = async context =>
        {
            var providerUserId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(providerUserId))
            {
                throw new InvalidOperationException("Google did not return a stable user identifier.");
            }

            var users = context.HttpContext.RequestServices.GetRequiredService<IUserAccountStore>();
            var pictureUrl = context.User.TryGetProperty("picture", out var pictureProperty) &&
                pictureProperty.ValueKind == System.Text.Json.JsonValueKind.String
                ? pictureProperty.GetString()
                : null;
            var account = await users.FindOrCreateFromExternalLoginAsync(
                new ExternalLoginProfile(
                    "google",
                    providerUserId,
                    context.Principal?.FindFirstValue(ClaimTypes.Email),
                    context.Principal?.FindFirstValue(ClaimTypes.Name),
                    pictureUrl),
                TimeProvider.System.GetUtcNow(),
                context.HttpContext.RequestAborted);

            if (context.Identity is ClaimsIdentity identity)
            {
                AccountClaims.AddAccountClaims(identity, account);
            }
        };
    });
}

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
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
    builder.Services.AddScoped<IUserAccountStore, PostgresUserAccountStore>();
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

app.UseForwardedHeaders();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
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
app.MapAccountEndpoints(persistenceEnabled);

app.Map("/ws", async context =>
    await context.RequestServices
        .GetRequiredService<WebSocketEndpoint>()
        .HandleAsync(context));

app.Run();
