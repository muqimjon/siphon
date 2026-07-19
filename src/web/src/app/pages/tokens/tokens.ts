import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ApiError } from '@shared/models';
import { Auth, CreatedToken, TokenUsage, TokenView } from '../../services/auth';
import { I18n } from '../../services/i18n';

@Component({
  selector: 'sp-tokens',
  changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './tokens.html',
  styleUrl: './tokens.css',
})
export class Tokens {
  readonly i18n = inject(I18n);
  readonly auth = inject(Auth);

  readonly tokens = signal<TokenView[]>([]);
  readonly created = signal<CreatedToken | null>(null);
  readonly showNew = signal(false);
  readonly name = signal('');
  readonly busy = signal(false);
  readonly copied = signal(false);
  readonly error = signal<string | null>(null);
  readonly usage = signal<TokenUsage[]>([]);

  readonly rows = computed(() =>
    this.tokens().map((t) => {
      const stat = this.usage().find((u) => u.tokenId === t.id);
      return { ...t, total: stat?.total ?? 0, today: stat?.today ?? 0 };
    }),
  );

  constructor() {
    this.load();
  }

  create(event: Event): void {
    event.preventDefault();
    const name = this.name().trim();
    if (!name) return;
    this.busy.set(true);
    this.error.set(null);
    this.auth
      .createToken(name)
      .then((token) => {
        this.created.set(token);
        this.name.set('');
        this.showNew.set(false);
        this.load();
      })
      .catch((err: ApiError) => this.error.set('err.' + err.code))
      .finally(() => this.busy.set(false));
  }

  dismiss(): void {
    this.created.set(null);
  }

  revoke(id: string): void {
    this.auth.deleteToken(id).then(() => this.load()).catch(() => {});
  }

  copy(secret: string): void {
    navigator.clipboard
      ?.writeText(secret)
      .then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 1500);
      })
      .catch(() => {});
  }

  fmtDate(iso: string | null): string {
    if (!iso) return this.i18n.t('dash.never');
    return new Date(iso).toLocaleDateString(this.i18n.lang());
  }

  private load(): void {
    this.auth.tokens().then((t) => this.tokens.set(t)).catch(() => {});
    this.auth.tokenUsage().then((u) => this.usage.set(u)).catch(() => {});
  }
}
