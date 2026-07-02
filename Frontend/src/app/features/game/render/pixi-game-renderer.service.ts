import { Injectable } from '@angular/core';
import type { Application, Container, Graphics } from 'pixi.js';

import { Board } from '../../../core/realtime/protocol.models';
import { ProjectedPoint } from '../netcode/projection-engine';
import {
  cellCenter,
  computeGameFieldLayout,
  GameFieldBounds,
  GameFieldLayout,
  pointTopLeft,
} from './game-field-layout';
import { emptyGameBoardRenderModel, GameFieldRenderer } from './game-field-renderer';
import { GameBoardRenderModel, RenderSnake } from './snake-sprite.projector';

type PixiModule = typeof import('pixi.js');

interface PixiSceneGraph {
  root: Container;
  backgroundGraphics: Graphics;
  tileGraphics: Graphics;
  gridGraphics: Graphics;
  foodGraphics: Graphics;
  snakeShadowGraphics: Graphics;
  snakeGraphics: Graphics;
  boardMask: Graphics;
}

@Injectable()
export class PixiGameRenderer extends GameFieldRenderer {
  private app: Application | null = null;
  private host: HTMLElement | null = null;
  private lastModel: GameBoardRenderModel | null = null;
  private scene: PixiSceneGraph | null = null;
  private pixiPromise: Promise<PixiModule> | null = null;

  async mount(host: HTMLElement): Promise<void> {
    this.host = host;

    if (this.app) {
      this.attachCanvas(host);
      this.resize();
      return;
    }

    const pixi = await this.loadPixi();
    const bounds = this.readHostBounds(host);
    const app = new pixi.Application();
    await app.init({
      width: Math.round(bounds.cssWidth),
      height: Math.round(bounds.cssHeight),
      resolution: this.devicePixelRatio(),
      autoDensity: true,
      autoStart: false,
      antialias: true,
      backgroundAlpha: 0,
      preference: 'webgl',
    });

    this.app = app;
    this.createSceneGraph(pixi);
    this.attachCanvas(host);
    this.drawEmpty();
  }

  resize(): void {
    if (!this.app || !this.host) {
      return;
    }

    const bounds = this.readHostBounds(this.host);
    this.app.renderer.resize(
      Math.round(bounds.cssWidth),
      Math.round(bounds.cssHeight),
      this.devicePixelRatio(),
    );

    if (this.lastModel) {
      this.render(this.lastModel);
    }
  }

  render(model: GameBoardRenderModel): void {
    this.lastModel = model;

    if (!this.app || !this.host) {
      return;
    }

    const bounds = this.readHostBounds(this.host);
    const scene = this.scene;
    if (!scene) {
      return;
    }

    const layout = computeGameFieldLayout(model.board, bounds);

    this.drawBoard(scene, layout, model.board);
    this.drawFood(scene, layout, model);
    this.drawSnakes(scene, layout, model.board, model.snakes);
    this.app.render();
  }

  drawEmpty(): void {
    this.render(emptyGameBoardRenderModel);
  }

  destroy(): void {
    this.lastModel = null;
    this.host = null;

    if (!this.app) {
      return;
    }

    this.app.destroy({ removeView: true }, { children: true });
    this.app = null;
    this.scene = null;
  }

  private createSceneGraph(pixi: PixiModule): void {
    const scene: PixiSceneGraph = {
      root: new pixi.Container(),
      backgroundGraphics: new pixi.Graphics(),
      tileGraphics: new pixi.Graphics(),
      gridGraphics: new pixi.Graphics(),
      foodGraphics: new pixi.Graphics(),
      snakeShadowGraphics: new pixi.Graphics(),
      snakeGraphics: new pixi.Graphics(),
      boardMask: new pixi.Graphics(),
    };
    // Pixi's stencil mask needs the graphics to remain renderable for the mask pass.
    scene.boardMask.renderable = true;

    scene.root.addChild(
      scene.backgroundGraphics,
      scene.tileGraphics,
      scene.gridGraphics,
      scene.foodGraphics,
      scene.snakeShadowGraphics,
      scene.snakeGraphics,
      scene.boardMask,
    );
    scene.snakeShadowGraphics.mask = scene.boardMask;
    scene.snakeGraphics.mask = scene.boardMask;
    this.scene = scene;

    if (this.app) {
      this.app.stage.addChild(scene.root);
    }
  }

  private attachCanvas(host: HTMLElement): void {
    if (!this.app) {
      return;
    }

    const canvas = this.app.canvas;
    canvas.className = 'game-board-pixi-canvas';
    canvas.setAttribute('aria-label', 'Snake board');
    canvas.style.display = 'block';
    canvas.style.width = '100%';
    canvas.style.height = '100%';

    if (canvas.parentElement !== host) {
      host.appendChild(canvas);
    }
  }

  private drawBoard(scene: PixiSceneGraph, layout: GameFieldLayout, board: Board): void {
    scene.backgroundGraphics
      .clear()
      .rect(0, 0, layout.cssWidth, layout.cssHeight)
      .fill('#f4f7f2');

    scene.tileGraphics.clear();
    for (let y = 0; y < board.height; y++) {
      for (let x = 0; x < board.width; x++) {
        scene.tileGraphics
          .rect(
            layout.originX + x * layout.tileSize,
            layout.originY + (board.height - 1 - y) * layout.tileSize,
            layout.tileSize,
            layout.tileSize,
          )
          .fill((x + y) % 2 === 0 ? '#eef6ef' : '#f8fbf7');
      }
    }

    scene.gridGraphics.clear();
    for (let x = 0; x <= board.width; x++) {
      const px = layout.originX + x * layout.tileSize;
      scene.gridGraphics.moveTo(px, layout.originY).lineTo(px, layout.originY + layout.boardHeight);
    }

    for (let y = 0; y <= board.height; y++) {
      const py = layout.originY + y * layout.tileSize;
      scene.gridGraphics.moveTo(layout.originX, py).lineTo(layout.originX + layout.boardWidth, py);
    }

    scene.gridGraphics
      .stroke({ color: '#24312b', alpha: 0.16, width: 1 })
      .rect(layout.originX, layout.originY, layout.boardWidth, layout.boardHeight)
      .stroke({ color: '#24312b', width: 2 });

    scene.boardMask
      .clear()
      .rect(layout.originX, layout.originY, layout.boardWidth, layout.boardHeight)
      .fill('#ffffff');
  }

  private drawFood(scene: PixiSceneGraph, layout: GameFieldLayout, model: GameBoardRenderModel): void {
    scene.foodGraphics.clear();

    for (const food of model.food) {
      const center = cellCenter(food.cell, layout, model.board);

      scene.foodGraphics
        .circle(center.x, center.y, layout.tileSize * 0.28)
        .fill(food.color)
        .stroke({ color: '#ffffff', alpha: 0.8, width: Math.max(1, layout.tileSize * 0.08) });
    }
  }

  private drawSnakes(
    scene: PixiSceneGraph,
    layout: GameFieldLayout,
    board: Board,
    snakes: RenderSnake[],
  ): void {
    scene.snakeShadowGraphics.clear();
    scene.snakeGraphics.clear();

    [...snakes]
      .sort((a, b) => Number(a.isLocal) - Number(b.isLocal))
      .forEach((snake) => this.drawSnake(scene, layout, board, snake));
  }

  private drawSnake(
    scene: PixiSceneGraph,
    layout: GameFieldLayout,
    board: Board,
    snake: RenderSnake,
  ): void {
    if (snake.segments.length === 0) {
      return;
    }

    const snakeAlpha = snake.alive ? 1 : 0.42;
    const inset = layout.tileSize * 0.11;
    const size = layout.tileSize - inset * 2;
    const radius = Math.min(layout.tileSize * 0.2, 8);

    [...snake.segments].reverse().forEach((segment, indexFromTail) => {
      const shouldStroke = snake.isLocal || indexFromTail === snake.segments.length - 1;

      this.drawWrappedSegment(layout, board, segment, ({ x, y }) => {
        scene.snakeShadowGraphics
          .roundRect(
            x + inset + layout.tileSize * 0.06,
            y + inset + layout.tileSize * 0.08,
            size,
            size,
            radius,
          )
          .fill({ color: '#111827', alpha: snake.alive ? 0.16 : 0.06 });

        scene.snakeGraphics
          .roundRect(x + inset, y + inset, size, size, radius)
          .fill({ color: snake.color, alpha: snakeAlpha });

        if (shouldStroke) {
          scene.snakeGraphics.stroke({
            color: '#111827',
            alpha: snake.isLocal ? 1 : 0.18,
            width: Math.max(1, layout.tileSize * (snake.isLocal ? 0.08 : 0.04)),
          });
        }
      });
    });
  }

  private drawWrappedSegment(
    layout: GameFieldLayout,
    board: Board,
    segment: ProjectedPoint,
    drawAt: (topLeft: ProjectedPoint) => void,
  ): void {
    for (const offsetY of [-board.height, 0, board.height]) {
      for (const offsetX of [-board.width, 0, board.width]) {
        drawAt(
          pointTopLeft(
            {
              x: segment.x + offsetX,
              y: segment.y + offsetY,
            },
            layout,
            board,
          ),
        );
      }
    }
  }

  private readHostBounds(host: HTMLElement): GameFieldBounds {
    const rect = host.getBoundingClientRect();

    return {
      cssWidth: Math.max(1, rect.width || host.clientWidth || 1),
      cssHeight: Math.max(1, rect.height || host.clientHeight || 1),
    };
  }

  private devicePixelRatio(): number {
    return typeof window === 'undefined' ? 1 : window.devicePixelRatio || 1;
  }

  private loadPixi(): Promise<PixiModule> {
    this.pixiPromise ??= import('pixi.js');

    return this.pixiPromise;
  }
}
