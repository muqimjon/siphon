import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './guards';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home/home').then((m) => m.Home) },
  { path: 'docs', loadComponent: () => import('./pages/docs/docs').then((m) => m.ApiDocs) },
  { path: 'bot', loadComponent: () => import('./pages/bot/bot').then((m) => m.BotGuide) },
  { path: 'login', loadComponent: () => import('./pages/login/login').then((m) => m.Login) },
  { path: 'register', loadComponent: () => import('./pages/register/register').then((m) => m.Register) },
  { path: 'dashboard', canActivate: [authGuard], loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard) },
  { path: 'tokens', canActivate: [authGuard], loadComponent: () => import('./pages/tokens/tokens').then((m) => m.Tokens) },
  { path: 'telegram', canActivate: [authGuard], loadComponent: () => import('./pages/telegram/telegram').then((m) => m.Telegram) },
  { path: 'account', canActivate: [authGuard], loadComponent: () => import('./pages/account/account').then((m) => m.Account) },
  { path: 'admin', canActivate: [adminGuard], loadComponent: () => import('./pages/admin/admin').then((m) => m.Admin) },
  { path: '**', redirectTo: '' },
];
