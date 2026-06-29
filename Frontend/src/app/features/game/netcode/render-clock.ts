export interface RenderPhaseInput {
  estimatedServerNow: number;
  matchStartServerTime: number;
  tickDurationMs: number;
  animationFramesPerTile: number;
}

export interface RenderPhase {
  continuousTick: number;
  currentTick: number;
  tileAlpha: number;
  visualFrameIndex: number;
  quantizedTileAlpha: number;
}

export function computeRenderPhase(input: RenderPhaseInput): RenderPhase {
  const tickDurationMs = Math.max(1, input.tickDurationMs);
  const animationFramesPerTile = Math.max(1, input.animationFramesPerTile);
  const elapsedMs = Math.max(0, input.estimatedServerNow - input.matchStartServerTime);
  const continuousTick = elapsedMs / tickDurationMs;
  const currentTick = Math.floor(continuousTick);
  const tileAlpha = continuousTick - currentTick;
  const visualFrameIndex = Math.min(
    animationFramesPerTile - 1,
    Math.floor(tileAlpha * animationFramesPerTile),
  );

  return {
    continuousTick,
    currentTick,
    tileAlpha,
    visualFrameIndex,
    quantizedTileAlpha: visualFrameIndex / animationFramesPerTile,
  };
}
