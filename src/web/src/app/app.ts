import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api } from './services/api';
import { Auth } from './services/auth';
import { I18n } from './services/i18n';
import { Theme } from './services/theme';

@Component({
  selector: 'sp-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  host: { '(document:click)': 'accountOpen.set(false)' },
})
export class App {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly i18n = inject(I18n);
  readonly theme = inject(Theme);
  readonly auth = inject(Auth);

  readonly health = signal<{ ok: boolean; version: string } | null>(null);
  readonly menuOpen = signal(false);
  readonly accountOpen = signal(false);

  readonly initial = computed(() => (this.auth.user()?.email ?? '?').charAt(0).toUpperCase());

  readonly healthText = computed(() => {
    const h = this.health();
    if (!h) return '';
    return h.ok ? this.i18n.t('health.ok', { version: h.version }) : this.i18n.t('health.offline');
  });

  constructor() {
    this.api
      .health()
      .then((h) => this.health.set({ ok: h.ok, version: h.version }))
      .catch(() => this.health.set({ ok: false, version: '' }));
  }

  logout(): void {
    this.auth.logout();
    this.menuOpen.set(false);
    this.accountOpen.set(false);
    this.router.navigateByUrl('/');
  }
}
