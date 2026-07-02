import { GameBoardRenderModel } from './snake-sprite.projector';

export const emptyGameBoardRenderModel: GameBoardRenderModel = {
  board: { width: 24, height: 18 },
  status: 'Running',
  localPlayerId: null,
  snakes: [],
  food: [],
};

export abstract class GameFieldRenderer {
  abstract mount(host: HTMLElement): Promise<void> | void;
  abstract resize(): void;
  abstract render(model: GameBoardRenderModel): void;
  abstract drawEmpty(): void;
  abstract destroy(): void;
}
