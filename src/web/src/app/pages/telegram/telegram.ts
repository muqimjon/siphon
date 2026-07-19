import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ApiError } from '@shared/models';
import { Auth, LinkToken, TelegramLinkView } from '../../services/auth';
import { I18n } from '../../services/i18n';
import { AccountNav } from '../account/account-nav';

@Component({
  selector: 'sp-telegram',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AccountNav],
  templateUrl: './telegram.html',
  styleUrl: './telegram.css',
})
export class Telegram {
  readonly i18n = inject(I18n);
  readonly auth = inject(Auth);

  readonly links = signal<TelegramLinkView[]>([]);
  readonly linkToken = signal<LinkToken | null>(null);
  readonly linkBusy = signal(false);
  readonly code = signal('');
  readonly codeError = signal<string | null>(null);

  constructor() {
    this.loadLinks();
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

  fmtDate(iso: string | null): string {
    if (!iso) return this.i18n.t('dash.never');
    return new Date(iso).toLocaleDateString(this.i18n.lang());
  }

  private loadLinks(): void {
    this.auth.telegramLinks().then((l) => this.links.set(l)).catch(() => {});
  }
}
