import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ServerMessageDispatcherService } from './core/realtime/server-message-dispatcher.service';
import { LeaderboardPanelComponent } from './features/leaderboard/leaderboard-panel.component';

@Component({
  selector: 'app-root',
  imports: [LeaderboardPanelComponent, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly dispatcher = inject(ServerMessageDispatcherService);

  constructor() {
    this.dispatcher.start();
  }
}
