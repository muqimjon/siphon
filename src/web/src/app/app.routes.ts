import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home/home').then((m) => m.Home) },
  { path: 'docs', loadComponent: () => import('./pages/docs/docs').then((m) => m.ApiDocs) },
  { path: 'bot', loadComponent: () => import('./pages/bot/bot').then((m) => m.BotGuide) },
  { path: '**', redirectTo: '' },
];
