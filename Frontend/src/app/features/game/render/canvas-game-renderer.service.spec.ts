import { TestBed } from '@angular/core/testing';

import { CanvasGameRenderer } from './canvas-game-renderer.service';
import { defaultSnakeColors } from './snake-sprite.projector';

describe('CanvasGameRenderer', () => {
  it('sizes the backing canvas for device pixel ratio and draws snakes and owned food', () => {
    const renderer = TestBed.inject(CanvasGameRenderer);
    const context = new RecordingCanvasContext();
    const canvas = createCanvas(context);

    renderer.draw(
      canvas,
      {
        board: { width: 8, height: 6 },
        localPlayerId: 'player-1',
        status: 'Running',
        snakes: [
          {
            playerId: 'player-1',
            alive: true,
            color: defaultSnakeColors[0],
            isLocal: true,
            segments: [
              { x: 2.4, y: 3 },
              { x: 1.4, y: 3 },
            ],
          },
        ],
        food: [
          {
            ownerPlayerId: 'player-1',
            cell: { x: 4, y: 1 },
            color: defaultSnakeColors[0],
          },
        ],
      },
      {
        cssWidth: 320,
        cssHeight: 240,
        devicePixelRatio: 2,
      },
    );

    expect(canvas.width).toBe(640);
    expect(canvas.height).toBe(480);
    expect(context.calls).toContainEqual({ name: 'setTransform', args: [2, 0, 0, 2, 0, 0] });
    expect(context.calls.some((call) => call.name === 'roundRect')).toBe(true);
    expect(context.calls.some((call) => call.name === 'arc')).toBe(true);
    expect(
      context.calls.some(
        (call) => call.name === 'fill' && call.fillStyle === defaultSnakeColors[0],
      ),
    ).toBe(true);
  });

  it('draws wrapped snake segments in translated copies clipped to the board', () => {
    const renderer = TestBed.inject(CanvasGameRenderer);
    const context = new RecordingCanvasContext();
    const canvas = createCanvas(context);

    renderer.draw(
      canvas,
      {
        board: { width: 8, height: 6 },
        localPlayerId: 'player-1',
        status: 'Running',
        snakes: [
          {
            playerId: 'player-1',
            alive: true,
            color: defaultSnakeColors[0],
            isLocal: true,
            segments: [{ x: 7.4, y: 3 }],
          },
        ],
        food: [],
      },
      {
        cssWidth: 320,
        cssHeight: 240,
        devicePixelRatio: 1,
      },
    );

    expect(context.calls.some((call) => call.name === 'clip')).toBe(true);
    expect(context.calls.filter((call) => call.name === 'roundRect').length).toBeGreaterThan(1);
  });
});

class RecordingCanvasContext {
  readonly calls: Array<{ name: string; args?: unknown[]; fillStyle?: string }> = [];

  private currentFillStyle = '';

  set fillStyle(value: string | CanvasGradient | CanvasPattern) {
    this.currentFillStyle = String(value);
  }

  get fillStyle(): string {
    return this.currentFillStyle;
  }

  strokeStyle: string | CanvasGradient | CanvasPattern = '';
  lineWidth = 1;
  globalAlpha = 1;
  lineCap: CanvasLineCap = 'butt';
  lineJoin: CanvasLineJoin = 'miter';

  setTransform(...args: unknown[]): void {
    this.calls.push({ name: 'setTransform', args });
  }

  clearRect(...args: unknown[]): void {
    this.calls.push({ name: 'clearRect', args });
  }

  fillRect(...args: unknown[]): void {
    this.calls.push({ name: 'fillRect', args, fillStyle: this.fillStyle });
  }

  strokeRect(...args: unknown[]): void {
    this.calls.push({ name: 'strokeRect', args });
  }

  rect(...args: unknown[]): void {
    this.calls.push({ name: 'rect', args });
  }

  clip(): void {
    this.calls.push({ name: 'clip' });
  }

  beginPath(): void {
    this.calls.push({ name: 'beginPath' });
  }

  moveTo(...args: unknown[]): void {
    this.calls.push({ name: 'moveTo', args });
  }

  lineTo(...args: unknown[]): void {
    this.calls.push({ name: 'lineTo', args });
  }

  roundRect(...args: unknown[]): void {
    this.calls.push({ name: 'roundRect', args });
  }

  arc(...args: unknown[]): void {
    this.calls.push({ name: 'arc', args, fillStyle: this.fillStyle });
  }

  fill(): void {
    this.calls.push({ name: 'fill', fillStyle: this.fillStyle });
  }

  stroke(): void {
    this.calls.push({ name: 'stroke' });
  }

  save(): void {
    this.calls.push({ name: 'save' });
  }

  restore(): void {
    this.calls.push({ name: 'restore' });
  }
}

function createCanvas(context: RecordingCanvasContext): HTMLCanvasElement {
  return {
    width: 0,
    height: 0,
    getContext: () => context,
    getBoundingClientRect: () =>
      ({
        width: 320,
        height: 240,
      }) as DOMRect,
  } as unknown as HTMLCanvasElement;
}
