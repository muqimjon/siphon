import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { ApiError } from '@shared/models';
import { Auth, ProviderInfo, TelegramAuth } from '../../services/auth';
import { I18n } from '../../services/i18n';

declare global {
  interface Window {
    onTelegramAuth?: (user: TelegramAuth) => void;
  }
}

@Component({
  selector: 'sp-providers',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './providers.html',
  styleUrl: './auth.css',
})
export class Providers {
  readonly i18n = inject(I18n);
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  readonly available = signal<ProviderInfo | null>(null);
  readonly telegramBot = signal('');
  readonly error = signal<string | null>(null);
  private readonly host = viewChild<ElementRef<HTMLDivElement>>('tgHost');

  constructor() {
    this.auth
      .providers()
      .then((p) => {
        this.available.set(p);
        if (p.telegram && p.botUsername) this.telegramBot.set(p.botUsername);
      })
      .catch(() => this.available.set({ google: false, github: false, telegram: false, botUsername: null }));
    effect(() => {
      const bot = this.telegramBot();
      const host = this.host()?.nativeElement;
      if (bot && host && host.childElementCount === 0) this.mountTelegram(bot, host);
    });
  }

  private mountTelegram(bot: string, host: HTMLDivElement): void {
    window.onTelegramAuth = (user) => this.onTelegram(user);
    const script = document.createElement('script');
    script.async = true;
    script.src = 'https://telegram.org/js/telegram-widget.js?22';
    script.setAttribute('data-telegram-login', bot);
    script.setAttribute('data-size', 'large');
    script.setAttribute('data-radius', '10');
    script.setAttribute('data-onauth', 'onTelegramAuth(user)');
    script.setAttribute('data-request-access', 'write');
    host.appendChild(script);
  }

  private onTelegram(user: TelegramAuth): void {
    this.error.set(null);
    this.auth
      .telegram(user)
      .then(() => this.router.navigateByUrl('/dashboard'))
      .catch((e: ApiError) => this.error.set(e.code));
  }
}
