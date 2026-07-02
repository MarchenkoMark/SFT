import { PixiGameRenderer } from './pixi-game-renderer.service';
import { GameBoardRenderModel } from './snake-sprite.projector';

const pixiMock = vi.hoisted(() => {
  class FakeContainer {
    children: unknown[] = [];
    mask: unknown = null;

    addChild(...children: unknown[]): void {
      this.children.push(...children);
    }
  }

  class FakeGraphics extends FakeContainer {
    operations: string[] = [];
    renderable = true;

    clear(): this {
      this.operations.push('clear');
      return this;
    }

    rect(): this {
      this.operations.push('rect');
      return this;
    }

    roundRect(): this {
      this.operations.push('roundRect');
      return this;
    }

    circle(): this {
      this.operations.push('circle');
      return this;
    }

    fill(): this {
      this.operations.push('fill');
      return this;
    }

    stroke(): this {
      this.operations.push('stroke');
      return this;
    }

    moveTo(): this {
      this.operations.push('moveTo');
      return this;
    }

    lineTo(): this {
      this.operations.push('lineTo');
      return this;
    }
  }

  class FakeApplication {
    canvas = document.createElement('canvas');
    stage = new FakeContainer();
    renderer = {
      resize: (...args: unknown[]) => {
        this.resizeCalls.push(args);
      },
    };
    initCalls: unknown[] = [];
    resizeCalls: unknown[][] = [];
    renderCount = 0;
    destroyCalls: unknown[][] = [];

    async init(options: unknown): Promise<void> {
      this.initCalls.push(options);
    }

    render(): void {
      this.renderCount += 1;
    }

    destroy(...args: unknown[]): void {
      this.destroyCalls.push(args);
    }
  }

  return {
    applications: [] as FakeApplication[],
    graphics: [] as FakeGraphics[],
    FakeApplication,
    FakeContainer,
    FakeGraphics,
  };
});

vi.mock('pixi.js', () => ({
  Application: class extends pixiMock.FakeApplication {
    constructor() {
      super();
      pixiMock.applications.push(this);
    }
  },
  Container: pixiMock.FakeContainer,
  Graphics: class extends pixiMock.FakeGraphics {
    constructor() {
      super();
      pixiMock.graphics.push(this);
    }
  },
}));

describe('PixiGameRenderer', () => {
  beforeEach(() => {
    pixiMock.applications.length = 0;
    pixiMock.graphics.length = 0;
  });

  it('keeps the board mask renderable and draws snake layers', async () => {
    const renderer = new PixiGameRenderer();
    const host = createHost(320, 240);

    await renderer.mount(host);
    renderer.render(createModelWithSnake());

    const [, , , , snakeShadowGraphics, snakeGraphics, boardMask] = pixiMock.graphics;
    expect(host.querySelector('canvas')).not.toBeNull();
    expect(boardMask.renderable).toBe(true);
    expect(snakeShadowGraphics.operations).toContain('roundRect');
    expect(snakeGraphics.operations).toContain('roundRect');
    expect(pixiMock.applications[0].renderCount).toBeGreaterThan(0);

    renderer.destroy();
  });
});

function createHost(width: number, height: number): HTMLElement {
  const host = document.createElement('div');

  Object.defineProperty(host, 'clientWidth', { configurable: true, value: width });
  Object.defineProperty(host, 'clientHeight', { configurable: true, value: height });
  host.getBoundingClientRect = () =>
    ({
      width,
      height,
      x: 0,
      y: 0,
      top: 0,
      right: width,
      bottom: height,
      left: 0,
      toJSON: () => ({}),
    }) as DOMRect;

  return host;
}

function createModelWithSnake(): GameBoardRenderModel {
  return {
    board: { width: 8, height: 6 },
    status: 'Running',
    localPlayerId: 'player-1',
    snakes: [
      {
        renderKey: 'snake:player-1',
        playerId: 'player-1',
        alive: true,
        isLocal: true,
        color: '#2f80ed',
        direction: 'Right',
        segments: [
          { x: 2, y: 3 },
          { x: 1, y: 3 },
        ],
        segmentKeys: ['snake:player-1:segment:0', 'snake:player-1:segment:1'],
      },
    ],
    food: [{ renderKey: 'food:player-1:4:1', ownerPlayerId: 'player-1', cell: { x: 4, y: 1 }, color: '#2f80ed' }],
  };
}
