import { Injectable } from '@angular/core';

import { TurnIntentAcceptedMessage } from '../../../core/realtime/protocol.models';
import { PredictedInput } from '../netcode/local-prediction';

@Injectable({ providedIn: 'root' })
export class LocalPredictionStore {
  private pendingInputs: PredictedInput[] = [];
  private sequence = 0;

  get snapshot(): PredictedInput[] {
    return this.pendingInputs;
  }

  inputsForPlayer(playerId: string): PredictedInput[] {
    return this.pendingInputs.filter((input) => input.playerId === playerId);
  }

  nextSequence(): number {
    this.sequence += 1;
    return this.sequence;
  }

  setPendingInputs(inputs: PredictedInput[]): void {
    this.pendingInputs = this.normalize(inputs);
  }

  setPendingInputsForPlayer(playerId: string, inputs: PredictedInput[]): void {
    this.pendingInputs = this.normalize([
      ...this.pendingInputs.filter((input) => input.playerId !== playerId),
      ...inputs,
    ]);
  }

  acceptServerIntent(message: TurnIntentAcceptedMessage): void {
    const acceptedInput: PredictedInput = {
      playerId: message.playerId,
      effectiveTick: message.effectiveTick,
      direction: message.direction,
      clientTime: message.clientTime,
      sequence: message.clientSequence,
      source: 'server',
    };

    this.pendingInputs = this.normalize([
      ...this.pendingInputs.filter((input) => !this.matchesAcceptedIntent(input, acceptedInput)),
      acceptedInput,
    ]);
  }

  removeCoveredInputs(authoritativeTick: number): void {
    this.pendingInputs = this.pendingInputs.filter(
      (input) => input.effectiveTick >= authoritativeTick,
    );
  }

  reset(): void {
    this.pendingInputs = [];
    this.sequence = 0;
  }

  private normalize(inputs: PredictedInput[]): PredictedInput[] {
    return [...inputs].sort(
      (a, b) =>
        a.playerId.localeCompare(b.playerId) ||
        a.effectiveTick - b.effectiveTick ||
        (a.sequence ?? 0) - (b.sequence ?? 0),
    );
  }

  private matchesAcceptedIntent(input: PredictedInput, acceptedInput: PredictedInput): boolean {
    if (input.playerId !== acceptedInput.playerId) {
      return false;
    }

    if (
      input.sequence !== null &&
      acceptedInput.sequence !== null &&
      input.sequence === acceptedInput.sequence
    ) {
      return true;
    }

    return (
      input.effectiveTick === acceptedInput.effectiveTick &&
      input.direction === acceptedInput.direction &&
      input.clientTime === acceptedInput.clientTime
    );
  }
}
