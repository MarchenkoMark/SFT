import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { NetworkDelayPanelComponent } from './core/realtime/network-delay-panel.component';
import { ServerMessageDispatcherService } from './core/realtime/server-message-dispatcher.service';

@Component({
  selector: 'app-root',
  imports: [NetworkDelayPanelComponent, RouterOutlet],
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
