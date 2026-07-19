import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError } from '@shared/models';
import { Auth, CreatedToken, TokenView } from '../../services/auth';
import { I18n } from '../../services/i18n';

@Component({
  selector: 'sp-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  readonly i18n = inject(I18n);
  readonly auth = inject(Auth);

  readonly tokens = signal<TokenView[]>([]);
  readonly created = signal<CreatedToken | null>(null);
  readonly showNew = signal(false);
  readonly name = signal('');
  readonly busy = signal(false);
  readonly copied = signal(false);
  readonly error = signal<string | null>(null);

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
      .catch((e: ApiError) => this.error.set(e.code))
      .finally(() => this.busy.set(false));
  }

  revoke(id: string): void {
    this.auth.deleteToken(id).then(() => this.load()).catch(() => {});
  }

  copy(secret: string): void {
    if (!navigator.clipboard?.writeText) return;
    navigator.clipboard
      .writeText(secret)
      .then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 1500);
      })
      .catch(() => {});
  }

  dismiss(): void {
    this.created.set(null);
  }

  fmtDate(iso: string | null): string {
    if (!iso) return this.i18n.t('dash.never');
    return new Date(iso).toLocaleDateString(this.i18n.lang());
  }

  private load(): void {
    this.auth.tokens().then((tokens) => this.tokens.set(tokens)).catch(() => {});
  }
}
