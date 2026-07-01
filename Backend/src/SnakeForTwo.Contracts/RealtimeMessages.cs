namespace SnakeForTwo.Contracts;

public enum DirectionDto
{
    Up,
    Right,
    Down,
    Left
}

public enum RoomStatusDto
{
    WaitingForPlayers,
    ReadyCheck,
    Starting,
    InGame,
    PostGame
}

public sealed record RoomPlayerDto(
    string PlayerId,
    int Seat,
    bool IsConnected,
    bool IsReady)
{
    public string? DisplayName { get; init; }
}

public sealed record RoomStateDto(
    string RoomId,
    RoomStatusDto Status,
    IReadOnlyList<RoomPlayerDto> Players,
    string? MatchId);

public sealed record TimingSettingsDto(
    double TilesPerSecond,
    int AnimationFramesPerTile,
    int TickDurationMs,
    int AnimationFrameDurationMs,
    int InputFutureBufferTicks,
    int DisconnectGracePeriodSeconds);

public abstract record ServerMessage
{
    public abstract string Type { get; }
}

public abstract record ClientMessage
{
    public abstract string Type { get; }
}

public sealed record CreateRoomClientMessage : ClientMessage
{
    public override string Type => "createRoom";
}

public sealed record JoinRoomClientMessage(string RoomId) : ClientMessage
{
    public override string Type => "joinRoom";
}

public sealed record ResumeRoomClientMessage(
    string RoomId,
    string PlayerSessionToken) : ClientMessage
{
    public override string Type => "resumeRoom";
}

public sealed record ReadyClientMessage(string RoomId) : ClientMessage
{
    public override string Type => "ready";
}

public sealed record UnreadyClientMessage(string RoomId) : ClientMessage
{
    public override string Type => "unready";
}

public sealed record LeaveRoomClientMessage(string RoomId) : ClientMessage
{
    public override string Type => "leaveRoom";
}

public sealed record ClientInputMessage(
    string RoomId,
    DirectionDto Direction,
    long ClientTime,
    int? ClientSequence = null) : ClientMessage
{
    public override string Type => "input";
}

public sealed record PingClientMessage(
    long ClientTime,
    string SampleId) : ClientMessage
{
    public override string Type => "ping";
}

public sealed record RoomCreatedMessage(
    string RoomId,
    string PlayerId,
    string PlayerSessionToken,
    RoomStateDto Room) : ServerMessage
{
    public override string Type => "roomCreated";
}

public sealed record RoomJoinedMessage(
    string RoomId,
    string PlayerId,
    string PlayerSessionToken,
    RoomStateDto Room) : ServerMessage
{
    public override string Type => "roomJoined";
}

public sealed record RoomResumedMessage(
    string RoomId,
    string PlayerId,
    string PlayerSessionToken,
    RoomStateDto Room) : ServerMessage
{
    public override string Type => "roomResumed";
}

public sealed record RoomStateMessage(RoomStateDto Room) : ServerMessage
{
    public override string Type => "roomState";
}

public sealed record GameStartingMessage(
    string RoomId,
    string MatchId,
    long StartServerTime,
    int TickRate,
    int Seed,
    TimingSettingsDto Timing) : ServerMessage
{
    public override string Type => "gameStarting";
}

public sealed record GameStartedMessage(
    string RoomId,
    string MatchId,
    string PlayerId,
    int Seat,
    long StartServerTime,
    int Seed,
    TimingSettingsDto Timing) : ServerMessage
{
    public override string Type => "gameStarted";
}

public sealed record AuthoritativeFrameMessage(
    string RoomId,
    string MatchId,
    long Tick,
    long ServerTime,
    string StateHash,
    AuthoritativeGameStateDto State) : ServerMessage
{
    public override string Type => "authoritativeFrame";
}

public sealed record CorrectionMessage(
    string RoomId,
    string MatchId,
    long Tick,
    long ServerTime,
    string StateHash,
    AuthoritativeGameStateDto State) : ServerMessage
{
    public override string Type => "correction";
}

public sealed record TurnIntentAcceptedMessage(
    string RoomId,
    string MatchId,
    string PlayerId,
    DirectionDto Direction,
    long EffectiveTick,
    long ClientTime,
    int? ClientSequence,
    long ServerReceivedAt) : ServerMessage
{
    public override string Type => "turnIntentAccepted";
}

public sealed record GameFinishedMessage(
    string RoomId,
    string MatchId,
    string Result,
    string Reason,
    AuthoritativeGameStateDto? FinalState) : ServerMessage
{
    public override string Type => "gameFinished";
}

public sealed record ErrorMessage(
    string Code,
    string Message,
    string? RoomId = null) : ServerMessage
{
    public override string Type => "error";
}

public sealed record PongMessage(
    long ClientTime,
    long ServerTime,
    string SampleId) : ServerMessage
{
    public override string Type => "pong";
}

public sealed record BoardDto(int Width, int Height);

public sealed record CellDto(int X, int Y);

public sealed record FoodItemDto(string OwnerPlayerId, CellDto Cell);

public sealed record AuthoritativeSnakeDto(
    string PlayerId,
    bool Alive,
    CellDto Head,
    DirectionDto Direction,
    IReadOnlyList<CellDto> Body);

public sealed record AuthoritativeGameStateDto(
    BoardDto Board,
    IReadOnlyList<AuthoritativeSnakeDto> Snakes,
    IReadOnlyList<FoodItemDto> Food,
    string Status);
