import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiError } from '@shared/models';
import { Auth } from '../../services/auth';
import { I18n } from '../../services/i18n';
import { Providers } from '../auth/providers';

@Component({
  selector: 'sp-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Providers],
  templateUrl: './register.html',
  styleUrl: '../auth/auth.css',
})
export class Register {
  readonly i18n = inject(I18n);
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  readonly email = signal('');
  readonly password = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  submit(event: Event): void {
    event.preventDefault();
    const email = this.email().trim();
    if (!email) return;
    this.busy.set(true);
    this.error.set(null);
    this.auth
      .register(email, this.password())
      .then(() => this.router.navigateByUrl('/dashboard'))
      .catch((e: ApiError) => this.error.set(e.code))
      .finally(() => this.busy.set(false));
  }
}
