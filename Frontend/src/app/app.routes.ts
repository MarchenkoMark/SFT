import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/lobby/welcome.component').then((component) => component.WelcomeComponent),
  },
  {
    path: 'join',
    loadComponent: () =>
      import('./features/lobby/join-route.component').then(
        (component) => component.JoinRouteComponent,
      ),
  },
  {
    path: 'room/:roomId',
    loadComponent: () =>
      import('./features/room/room-shell.component').then(
        (component) => component.RoomShellComponent,
      ),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
