import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError } from '@shared/models';
import { Auth, CreatedToken, LinkToken, TelegramLinkView, TokenView, Usage } from '../../services/auth';
import { I18n } from '../../services/i18n';

interface Bar {
  date: string;
  label: string;
  count: number;
  pct: number;
}

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
  readonly usage = signal<Usage | null>(null);
  readonly bars = signal<Bar[]>([]);
  readonly links = signal<TelegramLinkView[]>([]);
  readonly linkToken = signal<LinkToken | null>(null);
  readonly linkBusy = signal(false);
  readonly code = signal('');
  readonly codeError = signal<string | null>(null);

  constructor() {
    this.load();
    this.loadLinks();
    this.auth.usage(14).then((u) => this.applyUsage(u)).catch(() => {});
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

  startLink(): void {
    this.linkBusy.set(true);
    this.auth
      .createLinkToken()
      .then((t) => {
        this.linkToken.set(t);
        if (t.url) window.open(t.url, '_blank');
      })
      .catch(() => {})
      .finally(() => this.linkBusy.set(false));
  }

  connect(event: Event): void {
    event.preventDefault();
    const code = this.code().trim();
    if (code.length < 4) return;
    this.linkBusy.set(true);
    this.codeError.set(null);
    this.auth
      .connectTelegram(code)
      .then(() => {
        this.code.set('');
        this.loadLinks();
      })
      .catch((err: ApiError) => this.codeError.set('dash.' + (err.code === 'link-taken' ? 'telegramTaken' : 'telegramBadCode')))
      .finally(() => this.linkBusy.set(false));
  }

  refreshLinks(): void {
    this.loadLinks();
  }

  unlink(id: number): void {
    this.auth.unlinkTelegram(id).then(() => this.loadLinks()).catch(() => {});
  }

  linkName(link: TelegramLinkView): string {
    return link.username ? '@' + link.username : link.firstName || String(link.telegramUserId);
  }

  private loadLinks(): void {
    this.auth.telegramLinks().then((l) => this.links.set(l)).catch(() => {});
  }

  private load(): void {
    this.auth.tokens().then((tokens) => this.tokens.set(tokens)).catch(() => {});
  }

  private applyUsage(usage: Usage): void {
    this.usage.set(usage);
    const max = Math.max(1, ...usage.daily.map((d) => d.count));
    this.bars.set(
      usage.daily.map((d) => ({
        date: d.dateUtc,
        label: String(Number(d.dateUtc.slice(8))),
        count: d.count,
        pct: Math.round((d.count / max) * 100),
      })),
    );
  }
}
