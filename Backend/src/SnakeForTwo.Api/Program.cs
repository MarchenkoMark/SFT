using SnakeForTwo.Api;
using SnakeForTwo.Game.Application;
using SnakeForTwo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<GameRuntimeOptions>(
    builder.Configuration.GetSection(GameRuntimeOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRoomIdentityProvider, SecureRoomIdentityProvider>();
builder.Services.AddSingleton<IGameEventPublisher, InMemoryGameEventPublisher>();
builder.Services.AddSingleton<IRoomCoordinator>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<GameRuntimeOptions>>()
        .Value;
    var identityProvider = serviceProvider.GetRequiredService<IRoomIdentityProvider>();
    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

    return new GameRoomCoordinator(options, identityProvider, timeProvider);
});
builder.Services.AddSingleton<WebSocketConnectionRegistry>();
builder.Services.AddSingleton<WebSocketEndpoint>();
builder.Services.AddHostedService<RoomTimerHostedService>();

var app = builder.Build();

app.UseWebSockets();

app.MapGet("/", () => Results.Ok(new
{
    service = "SnakeForTwo.Api",
    status = "ready",
    websocket = "/ws"
}));

app.Map("/ws", async context =>
    await context.RequestServices
        .GetRequiredService<WebSocketEndpoint>()
        .HandleAsync(context));

app.Run();
