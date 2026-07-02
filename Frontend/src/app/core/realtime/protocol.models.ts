export type Direction = 'Up' | 'Right' | 'Down' | 'Left';

export type RoomStatus =
  | 'WaitingForPlayers'
  | 'ReadyCheck'
  | 'Starting'
  | 'InGame'
  | 'PostGame';

export interface RoomPlayer {
  playerId: string;
  seat: number;
  isConnected: boolean;
  isReady: boolean;
}

export interface RoomState {
  roomId: string;
  status: RoomStatus;
  players: RoomPlayer[];
  matchId: string | null;
}

export interface TimingSettings {
  tilesPerSecond: number;
  animationFramesPerTile: number;
  tickDurationMs: number;
  animationFrameDurationMs: number;
  inputFutureBufferTicks: number;
  disconnectGracePeriodSeconds: number;
}

export interface Cell {
  x: number;
  y: number;
}

export interface Board {
  width: number;
  height: number;
}

export interface FoodItem {
  ownerPlayerId: string;
  cell: Cell;
}

export interface AuthoritativeSnake {
  playerId: string;
  alive: boolean;
  head: Cell;
  direction: Direction;
  body: Cell[];
}

export type AuthoritativeGameStatus = 'Running' | 'Finished';

export interface AuthoritativeGameState {
  board: Board;
  snakes: AuthoritativeSnake[];
  food: FoodItem[];
  status: AuthoritativeGameStatus;
}

export type ClientMessage =
  | { type: 'createRoom' }
  | { type: 'joinRoom'; roomId: string }
  | { type: 'resumeRoom'; roomId: string; playerSessionToken: string }
  | { type: 'ready'; roomId: string }
  | { type: 'unready'; roomId: string }
  | { type: 'leaveRoom'; roomId: string }
  | {
      type: 'input';
      roomId: string;
      direction: Direction;
      clientTime: number;
      clientSequence: number;
    }
  | { type: 'ping'; clientTime: number; sampleId: string }
  | RenderDiagnosticsClientMessage;

export interface RoomCreatedMessage {
  type: 'roomCreated';
  roomId: string;
  playerId: string;
  playerSessionToken: string;
  room: RoomState;
}

export interface RoomJoinedMessage {
  type: 'roomJoined';
  roomId: string;
  playerId: string;
  playerSessionToken: string;
  room: RoomState;
}

export interface RoomResumedMessage {
  type: 'roomResumed';
  roomId: string;
  playerId: string;
  playerSessionToken: string;
  room: RoomState;
}

export interface RoomStateMessage {
  type: 'roomState';
  room: RoomState;
}

export interface GameStartingMessage {
  type: 'gameStarting';
  roomId: string;
  matchId: string;
  startServerTime: number;
  tickRate: number;
  seed: number;
  timing: TimingSettings;
}

export interface GameStartedMessage {
  type: 'gameStarted';
  roomId: string;
  matchId: string;
  playerId: string;
  seat: number;
  startServerTime: number;
  seed: number;
  timing: TimingSettings;
}

export interface AuthoritativeFrameMessage {
  type: 'authoritativeFrame';
  roomId: string;
  matchId: string;
  tick: number;
  serverTime: number;
  stateHash: string;
  state: AuthoritativeGameState;
}

export interface CorrectionMessage {
  type: 'correction';
  roomId: string;
  matchId: string;
  tick: number;
  serverTime: number;
  stateHash: string;
  state: AuthoritativeGameState;
}

export interface TurnIntentAcceptedMessage {
  type: 'turnIntentAccepted';
  roomId: string;
  matchId: string;
  playerId: string;
  direction: Direction;
  effectiveTick: number;
  clientTime: number;
  clientSequence: number | null;
  serverReceivedAt: number;
}

export interface GameFinishedMessage {
  type: 'gameFinished';
  roomId: string;
  matchId: string;
  result: string;
  reason: string;
  finalState: AuthoritativeGameState | null;
}

export interface RealtimeErrorMessage {
  type: 'error';
  code: string;
  message: string;
  roomId: string | null;
}

export interface PongMessage {
  type: 'pong';
  clientTime: number;
  serverTime: number;
  sampleId: string;
}

export interface RenderDiagnosticsPoint {
  x: number;
  y: number;
}

export interface RenderDiagnosticsSnakeSample {
  playerId: string;
  isLocal: boolean;
  alive: boolean;
  direction: Direction;
  projectedHead: RenderDiagnosticsPoint | null;
  authoritativeHead: Cell | null;
}

export interface RenderDiagnosticsFrame {
  frameIndex: number;
  capturedAt: number;
  performanceTime: number;
  latestFrameTick: number;
  latestFrameRevision: number;
  latestFrameSource: string;
  latestFrameServerTime: number;
  latestFrameReceivedAt: number;
  estimatedServerNow: number;
  renderContinuousTick: number;
  renderTick: number;
  tileAlpha: number;
  quantizedTileAlpha: number;
  renderTickDelta: number;
  frameServerLeadMs: number;
  receivedFrameLeadMs: number;
  estimatedServerOffsetMs: number;
  snakes: RenderDiagnosticsSnakeSample[];
}

export interface RenderDiagnosticsClientMessage {
  type: 'renderDiagnostics';
  roomId: string;
  matchId: string;
  localPlayerId: string | null;
  reason: string;
  clientSentAt: number;
  capturedWindowMs: number;
  totalRecordedFrameCount: number;
  sentFrameCount: number;
  frames: RenderDiagnosticsFrame[];
}

export type ServerMessage =
  | RoomCreatedMessage
  | RoomJoinedMessage
  | RoomResumedMessage
  | RoomStateMessage
  | GameStartingMessage
  | GameStartedMessage
  | AuthoritativeFrameMessage
  | CorrectionMessage
  | TurnIntentAcceptedMessage
  | GameFinishedMessage
  | RealtimeErrorMessage
  | PongMessage;

export type RoomAssignmentMessage =
  | RoomCreatedMessage
  | RoomJoinedMessage
  | RoomResumedMessage;
