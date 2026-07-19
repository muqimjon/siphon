import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api } from './services/api';
import { I18n } from './services/i18n';
import { Theme } from './services/theme';

@Component({
  selector: 'sp-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  private readonly api = inject(Api);
  readonly i18n = inject(I18n);
  readonly theme = inject(Theme);

  readonly health = signal<{ ok: boolean; version: string } | null>(null);
  readonly menuOpen = signal(false);

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
}
