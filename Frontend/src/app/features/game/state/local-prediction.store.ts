import { Injectable } from '@angular/core';

import { PredictedInput } from '../netcode/local-prediction';

@Injectable({ providedIn: 'root' })
export class LocalPredictionStore {
  private pendingInputs: PredictedInput[] = [];
  private sequence = 0;

  get snapshot(): PredictedInput[] {
    return this.pendingInputs;
  }

  nextSequence(): number {
    this.sequence += 1;
    return this.sequence;
  }

  setPendingInputs(inputs: PredictedInput[]): void {
    this.pendingInputs = [...inputs].sort((a, b) => a.targetTick - b.targetTick);
  }

  removeCoveredInputs(authoritativeTick: number): void {
    this.pendingInputs = this.pendingInputs.filter((input) => input.targetTick >= authoritativeTick);
  }

  reset(): void {
    this.pendingInputs = [];
    this.sequence = 0;
  }
}
