import { Injectable } from '@angular/core';

import { ClockSyncService } from '../../../core/clock/clock-sync.service';
import { Direction } from '../../../core/realtime/protocol.models';
import { RealtimeGatewayService } from '../../../core/realtime/realtime-gateway.service';
import { RoomQuery } from '../../room/room.query';

@Injectable({ providedIn: 'root' })
export class PlayerInputFacade {
  constructor(
    private readonly clockSync: ClockSyncService,
    private readonly gateway: RealtimeGatewayService,
    private readonly roomQuery: RoomQuery,
  ) {}

  sendDirection(direction: Direction): void {
    const roomId = this.roomQuery.snapshot.room?.roomId;
    if (!roomId) {
      return;
    }

    this.gateway.send({
      type: 'input',
      roomId,
      direction,
      clientTime: Math.round(this.clockSync.estimatedServerNow()),
    });
  }
}
