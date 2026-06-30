import {
  AuthoritativeGameState,
  AuthoritativeSnake,
  Board,
  Cell,
  Direction,
  FoodItem,
  RoomAssignmentMessage,
  RoomPlayer,
  RoomState,
  RoomStatus,
  ServerMessage,
  TimingSettings,
} from './protocol.models';

export interface DecodeServerMessageSuccess {
  ok: true;
  message: ServerMessage;
}

export interface DecodeServerMessageFailure {
  ok: false;
  reason: string;
  unknownType?: string;
}

export type DecodeServerMessageResult =
  | DecodeServerMessageSuccess
  | DecodeServerMessageFailure;

const roomStatuses = [
  'WaitingForPlayers',
  'ReadyCheck',
  'Starting',
  'InGame',
  'PostGame',
] as const;

const directions = ['Up', 'Right', 'Down', 'Left'] as const;
const gameStatuses = ['Running', 'Finished'] as const;

export function decodeServerMessage(payload: string): DecodeServerMessageResult {
  let parsed: unknown;

  try {
    parsed = JSON.parse(payload);
  } catch {
    return { ok: false, reason: 'Server message was not valid JSON.' };
  }

  if (!isRecord(parsed)) {
    return { ok: false, reason: 'Server message must be a JSON object.' };
  }

  const type = readString(parsed, 'type');
  if (!type) {
    return { ok: false, reason: 'Server message is missing a string type.' };
  }

  switch (type) {
    case 'roomCreated':
    case 'roomJoined':
    case 'roomResumed':
      return decodeRoomAssignment(parsed, type);
    case 'roomState':
      return decodeRoomStateMessage(parsed);
    case 'gameStarting':
      return decodeGameStarting(parsed);
    case 'gameStarted':
      return decodeGameStarted(parsed);
    case 'authoritativeFrame':
      return decodeAuthoritativeFrame(parsed, 'authoritativeFrame');
    case 'correction':
      return decodeAuthoritativeFrame(parsed, 'correction');
    case 'turnIntentAccepted':
      return decodeTurnIntentAccepted(parsed);
    case 'gameFinished':
      return decodeGameFinished(parsed);
    case 'error':
      return decodeError(parsed);
    case 'pong':
      return decodePong(parsed);
    default:
      return {
        ok: false,
        reason: `Unknown server message type '${type}'.`,
        unknownType: type,
      };
  }
}

function decodeRoomAssignment(
  record: Record<string, unknown>,
  type: RoomAssignmentMessage['type'],
): DecodeServerMessageResult {
  const roomId = readString(record, 'roomId');
  const playerId = readString(record, 'playerId');
  const playerSessionToken = readString(record, 'playerSessionToken');
  const room = parseRoomState(record['room']);

  if (!roomId || !playerId || !playerSessionToken || !room) {
    return { ok: false, reason: `${type} was missing required fields.` };
  }

  return {
    ok: true,
    message: {
      type,
      roomId,
      playerId,
      playerSessionToken,
      room,
    },
  };
}

function decodeRoomStateMessage(
  record: Record<string, unknown>,
): DecodeServerMessageResult {
  const room = parseRoomState(record['room']);
  if (!room) {
    return { ok: false, reason: 'roomState was missing a valid room.' };
  }

  return { ok: true, message: { type: 'roomState', room } };
}

function decodeGameStarting(
  record: Record<string, unknown>,
): DecodeServerMessageResult {
  const roomId = readString(record, 'roomId');
  const matchId = readString(record, 'matchId');
  const startServerTime = readNumber(record, 'startServerTime');
  const tickRate = readNumber(record, 'tickRate');
  const seed = readNumber(record, 'seed');
  const timing = parseTiming(record['timing']);

  if (!roomId || !matchId || startServerTime === undefined || tickRate === undefined || seed === undefined || !timing) {
    return { ok: false, reason: 'gameStarting was missing required fields.' };
  }

  return {
    ok: true,
    message: {
      type: 'gameStarting',
      roomId,
      matchId,
      startServerTime,
      tickRate,
      seed,
      timing,
    },
  };
}

function decodeGameStarted(
  record: Record<string, unknown>,
): DecodeServerMessageResult {
  const roomId = readString(record, 'roomId');
  const matchId = readString(record, 'matchId');
  const playerId = readString(record, 'playerId');
  const seat = readNumber(record, 'seat');
  const startServerTime = readNumber(record, 'startServerTime');
  const seed = readNumber(record, 'seed');
  const timing = parseTiming(record['timing']);

  if (
    !roomId ||
    !matchId ||
    !playerId ||
    seat === undefined ||
    startServerTime === undefined ||
    seed === undefined ||
    !timing
  ) {
    return { ok: false, reason: 'gameStarted was missing required fields.' };
  }

  return {
    ok: true,
    message: {
      type: 'gameStarted',
      roomId,
      matchId,
      playerId,
      seat,
      startServerTime,
      seed,
      timing,
    },
  };
}

function decodeAuthoritativeFrame(
  record: Record<string, unknown>,
  type: 'authoritativeFrame' | 'correction',
): DecodeServerMessageResult {
  const roomId = readString(record, 'roomId');
  const matchId = readString(record, 'matchId');
  const tick = readNumber(record, 'tick');
  const serverTime = readNumber(record, 'serverTime');
  const stateHash = readString(record, 'stateHash');
  const state = parseAuthoritativeGameState(record['state']);

  if (!roomId || !matchId || tick === undefined || serverTime === undefined || !stateHash || !state) {
    return { ok: false, reason: `${type} was missing required fields.` };
  }

  return {
    ok: true,
    message: {
      type,
      roomId,
      matchId,
      tick,
      serverTime,
      stateHash,
      state,
    },
  };
}

function decodeTurnIntentAccepted(record: Record<string, unknown>): DecodeServerMessageResult {
  const roomId = readString(record, 'roomId');
  const matchId = readString(record, 'matchId');
  const playerId = readString(record, 'playerId');
  const direction = readEnum<Direction>(record, 'direction', directions);
  const effectiveTick = readNumber(record, 'effectiveTick');
  const clientTime = readNumber(record, 'clientTime');
  const clientSequence = readOptionalNumber(record, 'clientSequence');
  const serverReceivedAt = readNumber(record, 'serverReceivedAt');

  if (
    !roomId ||
    !matchId ||
    !playerId ||
    !direction ||
    effectiveTick === undefined ||
    clientTime === undefined ||
    clientSequence === undefined ||
    serverReceivedAt === undefined
  ) {
    return { ok: false, reason: 'turnIntentAccepted was missing required fields.' };
  }

  return {
    ok: true,
    message: {
      type: 'turnIntentAccepted',
      roomId,
      matchId,
      playerId,
      direction,
      effectiveTick,
      clientTime,
      clientSequence,
      serverReceivedAt,
    },
  };
}

function decodeGameFinished(
  record: Record<string, unknown>,
): DecodeServerMessageResult {
  const roomId = readString(record, 'roomId');
  const matchId = readString(record, 'matchId');
  const result = readString(record, 'result');
  const reason = readString(record, 'reason');
  const finalStateValue = record['finalState'];
  const finalState =
    finalStateValue === null || finalStateValue === undefined
      ? null
      : parseAuthoritativeGameState(finalStateValue);

  if (!roomId || !matchId || !result || !reason || finalState === undefined) {
    return { ok: false, reason: 'gameFinished was missing required fields.' };
  }

  return {
    ok: true,
    message: {
      type: 'gameFinished',
      roomId,
      matchId,
      result,
      reason,
      finalState,
    },
  };
}

function decodeError(record: Record<string, unknown>): DecodeServerMessageResult {
  const code = readString(record, 'code');
  const message = readString(record, 'message');
  const roomId = readOptionalString(record, 'roomId');

  if (!code || !message) {
    return { ok: false, reason: 'error was missing code or message.' };
  }

  return {
    ok: true,
    message: {
      type: 'error',
      code,
      message,
      roomId,
    },
  };
}

function decodePong(record: Record<string, unknown>): DecodeServerMessageResult {
  const clientTime = readNumber(record, 'clientTime');
  const serverTime = readNumber(record, 'serverTime');
  const sampleId = readString(record, 'sampleId');

  if (clientTime === undefined || serverTime === undefined || !sampleId) {
    return { ok: false, reason: 'pong was missing required fields.' };
  }

  return {
    ok: true,
    message: {
      type: 'pong',
      clientTime,
      serverTime,
      sampleId,
    },
  };
}

function parseRoomState(value: unknown): RoomState | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const roomId = readString(value, 'roomId');
  const status = readEnum(value, 'status', roomStatuses);
  const playersValue = value['players'];
  const matchId = readOptionalString(value, 'matchId');

  if (!roomId || !status || !Array.isArray(playersValue)) {
    return undefined;
  }

  const players: RoomPlayer[] = [];
  for (const playerValue of playersValue) {
    const player = parseRoomPlayer(playerValue);
    if (!player) {
      return undefined;
    }

    players.push(player);
  }

  return { roomId, status, players, matchId };
}

function parseRoomPlayer(value: unknown): RoomPlayer | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const playerId = readString(value, 'playerId');
  const seat = readNumber(value, 'seat');
  const isConnected = readBoolean(value, 'isConnected');
  const isReady = readBoolean(value, 'isReady');

  if (!playerId || seat === undefined || isConnected === undefined || isReady === undefined) {
    return undefined;
  }

  return { playerId, seat, isConnected, isReady };
}

function parseTiming(value: unknown): TimingSettings | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const tilesPerSecond = readNumber(value, 'tilesPerSecond');
  const animationFramesPerTile = readNumber(value, 'animationFramesPerTile');
  const tickDurationMs = readNumber(value, 'tickDurationMs');
  const animationFrameDurationMs = readNumber(value, 'animationFrameDurationMs');
  const inputFutureBufferTicks = readNumber(value, 'inputFutureBufferTicks');
  const disconnectGracePeriodSeconds = readNumber(value, 'disconnectGracePeriodSeconds');

  if (
    tilesPerSecond === undefined ||
    animationFramesPerTile === undefined ||
    tickDurationMs === undefined ||
    animationFrameDurationMs === undefined ||
    inputFutureBufferTicks === undefined ||
    disconnectGracePeriodSeconds === undefined
  ) {
    return undefined;
  }

  return {
    tilesPerSecond,
    animationFramesPerTile,
    tickDurationMs,
    animationFrameDurationMs,
    inputFutureBufferTicks,
    disconnectGracePeriodSeconds,
  };
}

function parseAuthoritativeGameState(value: unknown): AuthoritativeGameState | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const board = parseBoard(value['board']);
  const snakesValue = value['snakes'];
  const foodValue = value['food'];
  const status = readEnum(value, 'status', gameStatuses);

  if (!board || !Array.isArray(snakesValue) || !Array.isArray(foodValue) || !status) {
    return undefined;
  }

  const snakes: AuthoritativeSnake[] = [];
  for (const snakeValue of snakesValue) {
    const snake = parseSnake(snakeValue);
    if (!snake) {
      return undefined;
    }

    snakes.push(snake);
  }

  const food: FoodItem[] = [];
  for (const foodItemValue of foodValue) {
    const item = parseFoodItem(foodItemValue);
    if (!item) {
      return undefined;
    }

    food.push(item);
  }

  return { board, snakes, food, status };
}

function parseBoard(value: unknown): Board | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const width = readNumber(value, 'width');
  const height = readNumber(value, 'height');

  if (width === undefined || height === undefined) {
    return undefined;
  }

  return { width, height };
}

function parseCell(value: unknown): Cell | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const x = readNumber(value, 'x');
  const y = readNumber(value, 'y');

  if (x === undefined || y === undefined) {
    return undefined;
  }

  return { x, y };
}

function parseFoodItem(value: unknown): FoodItem | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const ownerPlayerId = readString(value, 'ownerPlayerId');
  const cell = parseCell(value['cell']);

  if (!ownerPlayerId || !cell) {
    return undefined;
  }

  return { ownerPlayerId, cell };
}

function parseSnake(value: unknown): AuthoritativeSnake | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const playerId = readString(value, 'playerId');
  const alive = readBoolean(value, 'alive');
  const head = parseCell(value['head']);
  const direction = readEnum<Direction>(value, 'direction', directions);
  const bodyValue = value['body'];

  if (!playerId || alive === undefined || !head || !direction || !Array.isArray(bodyValue)) {
    return undefined;
  }

  const body: Cell[] = [];
  for (const cellValue of bodyValue) {
    const cell = parseCell(cellValue);
    if (!cell) {
      return undefined;
    }

    body.push(cell);
  }

  return { playerId, alive, head, direction, body };
}

function readString(record: Record<string, unknown>, propertyName: string): string | undefined {
  const value = record[propertyName];
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined;
}

function readOptionalString(record: Record<string, unknown>, propertyName: string): string | null {
  const value = record[propertyName];
  if (value === null || value === undefined) {
    return null;
  }

  return typeof value === 'string' ? value : null;
}

function readOptionalNumber(
  record: Record<string, unknown>,
  propertyName: string,
): number | null | undefined {
  const value = record[propertyName];
  if (value === null || value === undefined) {
    return null;
  }

  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

function readNumber(record: Record<string, unknown>, propertyName: string): number | undefined {
  const value = record[propertyName];
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

function readBoolean(record: Record<string, unknown>, propertyName: string): boolean | undefined {
  const value = record[propertyName];
  return typeof value === 'boolean' ? value : undefined;
}

function readEnum<T extends string>(
  record: Record<string, unknown>,
  propertyName: string,
  allowed: readonly T[],
): T | undefined {
  const value = readString(record, propertyName);
  return value && allowed.includes(value as T) ? (value as T) : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
