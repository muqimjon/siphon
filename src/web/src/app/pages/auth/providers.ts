import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { ApiError } from '@shared/models';
import { Auth, TelegramAuth } from '../../services/auth';
import { I18n } from '../../services/i18n';

const TELEGRAM_BOT = 'siphon_bot';

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

  readonly telegramBot = TELEGRAM_BOT;
  readonly error = signal<string | null>(null);
  private readonly host = viewChild<ElementRef<HTMLDivElement>>('tgHost');

  constructor() {
    afterNextRender(() => this.mountTelegram());
  }

  private mountTelegram(): void {
    const host = this.host()?.nativeElement;
    if (!host) return;
    window.onTelegramAuth = (user) => this.onTelegram(user);
    const script = document.createElement('script');
    script.async = true;
    script.src = 'https://telegram.org/js/telegram-widget.js?22';
    script.setAttribute('data-telegram-login', this.telegramBot);
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
