import { Injectable } from '@angular/core';

import { Board } from '../../../core/realtime/protocol.models';
import { ProjectedPoint } from '../netcode/projection-engine';
import {
  cellCenter,
  computeGameFieldLayout,
  GameFieldLayout,
  pointTopLeft,
} from './game-field-layout';
import { GameBoardRenderModel, RenderFood, RenderSnake } from './snake-sprite.projector';

export interface CanvasRendererOptions {
  cssWidth?: number;
  cssHeight?: number;
  devicePixelRatio?: number;
}

@Injectable({ providedIn: 'root' })
export class CanvasGameRenderer {
  draw(
    canvas: HTMLCanvasElement,
    model: GameBoardRenderModel,
    options: CanvasRendererOptions = {},
  ): void {
    const context = canvas.getContext('2d');
    if (!context) {
      return;
    }

    const layout = this.prepareCanvas(canvas, context, model.board, options);
    this.drawBoard(context, layout, model.board);
    model.food.forEach((food) => this.drawFood(context, layout, model.board, food));
    this.drawSnakes(context, layout, model.board, model.snakes);
  }

  drawEmpty(canvas: HTMLCanvasElement, options: CanvasRendererOptions = {}): void {
    this.draw(
      canvas,
      {
        board: { width: 24, height: 18 },
        status: 'Running',
        localPlayerId: null,
        snakes: [],
        food: [],
      },
      options,
    );
  }

  private prepareCanvas(
    canvas: HTMLCanvasElement,
    context: CanvasRenderingContext2D,
    board: Board,
    options: CanvasRendererOptions,
  ): GameFieldLayout {
    const rect = canvas.getBoundingClientRect();
    const cssWidth = Math.max(1, options.cssWidth ?? rect.width ?? canvas.clientWidth ?? 1);
    const cssHeight = Math.max(1, options.cssHeight ?? rect.height ?? canvas.clientHeight ?? 1);
    const devicePixelRatio =
      options.devicePixelRatio ?? (typeof window === 'undefined' ? 1 : window.devicePixelRatio) ?? 1;
    const backingWidth = Math.round(cssWidth * devicePixelRatio);
    const backingHeight = Math.round(cssHeight * devicePixelRatio);

    if (canvas.width !== backingWidth) {
      canvas.width = backingWidth;
    }

    if (canvas.height !== backingHeight) {
      canvas.height = backingHeight;
    }

    context.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
    context.clearRect(0, 0, cssWidth, cssHeight);

    return computeGameFieldLayout(board, {
      cssWidth,
      cssHeight,
    });
  }

  private drawBoard(
    context: CanvasRenderingContext2D,
    layout: GameFieldLayout,
    board: Board,
  ): void {
    context.fillStyle = '#f4f7f2';
    context.fillRect(0, 0, layout.cssWidth, layout.cssHeight);

    for (let y = 0; y < board.height; y++) {
      for (let x = 0; x < board.width; x++) {
        context.fillStyle = (x + y) % 2 === 0 ? '#eef6ef' : '#f8fbf7';
        context.fillRect(
          layout.originX + x * layout.tileSize,
          layout.originY + (board.height - 1 - y) * layout.tileSize,
          layout.tileSize,
          layout.tileSize,
        );
      }
    }

    context.beginPath();
    context.strokeStyle = 'rgba(36, 49, 43, 0.16)';
    context.lineWidth = 1;

    for (let x = 0; x <= board.width; x++) {
      const px = layout.originX + x * layout.tileSize;
      context.moveTo(px, layout.originY);
      context.lineTo(px, layout.originY + layout.boardHeight);
    }

    for (let y = 0; y <= board.height; y++) {
      const py = layout.originY + y * layout.tileSize;
      context.moveTo(layout.originX, py);
      context.lineTo(layout.originX + layout.boardWidth, py);
    }

    context.stroke();
    context.strokeStyle = '#24312b';
    context.lineWidth = 2;
    context.strokeRect(layout.originX, layout.originY, layout.boardWidth, layout.boardHeight);
  }

  private drawFood(
    context: CanvasRenderingContext2D,
    layout: GameFieldLayout,
    board: Board,
    food: RenderFood,
  ): void {
    const center = cellCenter(food.cell, layout, board);

    context.save();
    context.fillStyle = food.color;
    context.beginPath();
    context.arc(center.x, center.y, layout.tileSize * 0.28, 0, Math.PI * 2);
    context.fill();
    context.strokeStyle = 'rgba(255, 255, 255, 0.8)';
    context.lineWidth = Math.max(1, layout.tileSize * 0.08);
    context.stroke();
    context.restore();
  }

  private drawSnakes(
    context: CanvasRenderingContext2D,
    layout: GameFieldLayout,
    board: Board,
    snakes: RenderSnake[],
  ): void {
    context.save();
    context.beginPath();
    context.rect(layout.originX, layout.originY, layout.boardWidth, layout.boardHeight);
    context.clip();

    [...snakes]
      .sort((a, b) => Number(a.isLocal) - Number(b.isLocal))
      .forEach((snake) => this.drawSnake(context, layout, board, snake));

    context.restore();
  }

  private drawSnake(
    context: CanvasRenderingContext2D,
    layout: GameFieldLayout,
    board: Board,
    snake: RenderSnake,
  ): void {
    if (snake.segments.length === 0) {
      return;
    }

    context.save();
    context.globalAlpha = snake.alive ? 1 : 0.42;
    context.fillStyle = snake.color;
    context.strokeStyle = snake.isLocal ? '#111827' : 'rgba(17, 24, 39, 0.18)';
    context.lineWidth = Math.max(1, layout.tileSize * (snake.isLocal ? 0.08 : 0.04));

    [...snake.segments].reverse().forEach((segment, indexFromTail) =>
      this.drawWrappedSegment(context, layout, board, segment, snake, indexFromTail),
    );

    context.restore();
  }

  private drawWrappedSegment(
    context: CanvasRenderingContext2D,
    layout: GameFieldLayout,
    board: Board,
    segment: ProjectedPoint,
    snake: RenderSnake,
    indexFromTail: number,
  ): void {
    const inset = layout.tileSize * 0.11;
    const size = layout.tileSize - inset * 2;
    const radius = Math.min(layout.tileSize * 0.2, 8);
    const shouldStroke = snake.isLocal || indexFromTail === snake.segments.length - 1;

    for (const offsetY of [-board.height, 0, board.height]) {
      for (const offsetX of [-board.width, 0, board.width]) {
        const topLeft = pointTopLeft(
          {
            x: segment.x + offsetX,
            y: segment.y + offsetY,
          },
          layout,
          board,
        );

        context.beginPath();
        context.roundRect(topLeft.x + inset, topLeft.y + inset, size, size, radius);
        context.fill();

        if (shouldStroke) {
          context.stroke();
        }
      }
    }
  }
}
