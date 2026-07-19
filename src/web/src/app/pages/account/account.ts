import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ApiError } from '@shared/models';
import { Auth, ProfileView, ProviderInfo } from '../../services/auth';
import { I18n } from '../../services/i18n';
import { AccountNav } from './account-nav';

const OAUTH = ['google', 'github'] as const;

@Component({
  selector: 'sp-account',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AccountNav],
  templateUrl: './account.html',
  styleUrl: './account.css',
})
export class Account {
  readonly i18n = inject(I18n);
  readonly auth = inject(Auth);

  readonly profile = signal<ProfileView | null>(null);
  readonly available = signal<ProviderInfo | null>(null);

  readonly firstName = signal('');
  readonly lastName = signal('');
  readonly userName = signal('');
  readonly savedName = signal(false);
  readonly nameError = signal<string | null>(null);

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly savedPassword = signal(false);
  readonly passwordError = signal<string | null>(null);

  readonly busy = signal(false);

  readonly oauth = computed(() => {
    const info = this.available();
    const linked = this.profile()?.logins.map((l) => l.provider) ?? [];
    return OAUTH.filter((p) => info?.[p]).map((p) => ({ provider: p, linked: linked.includes(p) }));
  });

  readonly canUnlink = computed(() => {
    const p = this.profile();
    if (!p) return false;
    return p.hasPassword || p.logins.length > 1;
  });

  constructor() {
    this.load();
    this.auth.providers().then((p) => this.available.set(p)).catch(() => {});
  }

  saveName(event: Event): void {
    event.preventDefault();
    this.busy.set(true);
    this.nameError.set(null);
    this.savedName.set(false);
    this.auth
      .updateProfile(this.firstName().trim(), this.lastName().trim(), this.userName().trim())
      .then((p) => {
        this.apply(p);
        this.savedName.set(true);
      })
      .catch((err: ApiError) => this.nameError.set('account.' + (err.code === 'name-taken' ? 'nameTaken' : 'nameInvalid')))
      .finally(() => this.busy.set(false));
  }

  savePassword(event: Event): void {
    event.preventDefault();
    const next = this.newPassword();
    if (next.length < 8) {
      this.passwordError.set('account.passwordWeak');
      return;
    }
    this.busy.set(true);
    this.passwordError.set(null);
    this.savedPassword.set(false);
    this.auth
      .setPassword(this.profile()?.hasPassword ? this.currentPassword() : null, next)
      .then(() => {
        this.currentPassword.set('');
        this.newPassword.set('');
        this.savedPassword.set(true);
        this.load();
      })
      .catch(() => this.passwordError.set('account.passwordRejected'))
      .finally(() => this.busy.set(false));
  }

  link(provider: string): void {
    this.auth.linkProvider(provider).catch(() => {});
  }

  unlink(provider: string): void {
    this.auth.unlinkLogin(provider).then(() => this.load()).catch(() => {});
  }

  providerLabel(provider: string): string {
    return provider.charAt(0).toUpperCase() + provider.slice(1);
  }

  fmtDate(iso: string): string {
    return new Date(iso).toLocaleDateString(this.i18n.lang());
  }

  private load(): void {
    this.auth.profile().then((p) => this.apply(p)).catch(() => {});
  }

  private apply(p: ProfileView): void {
    this.profile.set(p);
    this.firstName.set(p.firstName ?? '');
    this.lastName.set(p.lastName ?? '');
    this.userName.set(p.userName ?? '');
  }
}
