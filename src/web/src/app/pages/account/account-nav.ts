import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Auth } from '../../services/auth';
import { I18n } from '../../services/i18n';

@Component({
  selector: 'sp-account-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="account-nav">
      <a routerLink="/dashboard" routerLinkActive="active">{{ i18n.t('nav.dashboard') }}</a>
      <a routerLink="/tokens" routerLinkActive="active">{{ i18n.t('nav.tokens') }}</a>
      <a routerLink="/telegram" routerLinkActive="active">{{ i18n.t('nav.telegram') }}</a>
      <a routerLink="/account" routerLinkActive="active">{{ i18n.t('nav.account') }}</a>
      @if (auth.isAdmin()) {
        <a routerLink="/admin" routerLinkActive="active">{{ i18n.t('nav.admin') }}</a>
      }
    </nav>
  `,
  styles: [
    `
      .account-nav {
        display: flex; gap: var(--sp-1); flex-wrap: wrap;
        margin-bottom: var(--sp-6); padding-bottom: var(--sp-3);
        border-bottom: 1px solid var(--sp-border);
      }
      .account-nav a {
        font-size: var(--sp-fs-sm); font-weight: 600; color: var(--sp-text-dim);
        text-decoration: none; padding: var(--sp-2) var(--sp-4); border-radius: var(--sp-r-pill);
        transition: background var(--sp-t-fast) var(--sp-ease), color var(--sp-t-fast) var(--sp-ease);
      }
      .account-nav a:hover { color: var(--sp-text); background: var(--sp-surface-2); }
      .account-nav a.active { color: var(--sp-accent-fg); background: var(--sp-accent); }
    `,
  ],
})
export class AccountNav {
  readonly i18n = inject(I18n);
  readonly auth = inject(Auth);
}
