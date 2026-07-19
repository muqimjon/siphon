import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiError } from '@shared/models';
import { Auth } from '../../services/auth';
import { I18n } from '../../services/i18n';
import { Providers } from '../auth/providers';

@Component({
  selector: 'sp-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Providers],
  templateUrl: './login.html',
  styleUrl: '../auth/auth.css',
})
export class Login {
  readonly i18n = inject(I18n);
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  readonly email = signal('');
  readonly password = signal('');
  readonly code = signal('');
  readonly otpSent = signal(false);
  readonly showPassword = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  sendCode(event: Event): void {
    event.preventDefault();
    const email = this.email().trim();
    if (!email) return;
    this.run(this.auth.emailStart(email), () => this.otpSent.set(true));
  }

  verifyCode(event: Event): void {
    event.preventDefault();
    this.run(this.auth.emailVerify(this.email().trim(), this.code().trim()), () => this.done());
  }

  passwordLogin(event: Event): void {
    event.preventDefault();
    this.run(this.auth.login(this.email().trim(), this.password()), () => this.done());
  }

  private done(): void {
    this.router.navigateByUrl('/dashboard');
  }

  private run(work: Promise<unknown>, ok: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    work
      .then(() => ok())
      .catch((e: ApiError) => this.error.set(e.code))
      .finally(() => this.busy.set(false));
  }
}
