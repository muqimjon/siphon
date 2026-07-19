import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiError } from '@shared/models';

const BASE = '/api/v1';
const JWT_KEY = 'siphon.jwt';

export interface Plan {
  name: string;
  dailyRequests: number;
  maxConcurrent: number;
  maxFileSizeMb: number;
}

export interface Me {
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  role: string;
  plan: Plan;
  providers: string[];
}

export interface TokenView {
  id: string;
  name: string;
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  revoked: boolean;
}

export interface CreatedToken {
  id: string;
  name: string;
  prefix: string;
  secret: string;
}

export interface UsageRow {
  userId: string;
  dateUtc: string;
  count: number;
}

export interface Limits {
  maxFileSizeMb: number;
  dailyRequests: number;
  maxConcurrent: number;
  fileSizeLimitMbOverride: number | null;
  dailyRequestLimitOverride: number | null;
  concurrentLimitOverride: number | null;
  overridesExpireAt: string | null;
  overridesActive: boolean;
}

export interface LoginView {
  provider: string;
  displayName: string;
}

export interface ProfileView {
  email: string | null;
  firstName: string | null;
  lastName: string | null;
  userName: string | null;
  role: string;
  createdAt: string;
  hasPassword: boolean;
  logins: LoginView[];
}

export interface TokenUsage {
  tokenId: string | null;
  name: string | null;
  prefix: string | null;
  total: number;
  today: number;
  bytes: number;
}

export interface ProviderInfo {
  google: boolean;
  github: boolean;
  telegram: boolean;
  botUsername: string | null;
}

export interface TelegramLinkView {
  telegramUserId: number;
  username: string | null;
  firstName: string | null;
  linkedAtUtc: string;
}

export interface LinkToken {
  token: string;
  url: string;
  expiresUtc: string;
}

export interface Usage {
  daily: { dateUtc: string; count: number }[];
  limits: Limits;
  usedToday: number;
}

export interface AdminUser {
  id: string;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
  role: string;
  plan: string;
  createdAt: string;
  totalUsage: number;
  limits: Limits;
}

export interface TelegramAuth {
  id: number;
  first_name?: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
  auth_date: number;
  hash: string;
}

@Injectable({ providedIn: 'root' })
export class Auth {
  readonly token = signal<string | null>(localStorage.getItem(JWT_KEY));
  readonly user = signal<Me | null>(null);
  readonly isAdmin = computed(() => this.user()?.role === 'admin');

  constructor() {
    const match = location.hash.match(/token=([^&]+)/);
    if (match?.[1]) {
      this.store(decodeURIComponent(match[1]));
      history.replaceState(null, '', location.pathname + location.search);
    }
    if (this.token()) this.loadMe();
  }

  register(email: string, password: string): Promise<void> {
    return this.authenticate('/auth/register', { email, password });
  }

  login(email: string, password: string): Promise<void> {
    return this.authenticate('/auth/login', { email, password });
  }

  emailStart(email: string): Promise<void> {
    return this.request<void>('POST', '/auth/email/start', { email });
  }

  emailVerify(email: string, code: string): Promise<void> {
    return this.authenticate('/auth/email/verify', { email, code });
  }

  telegram(payload: TelegramAuth): Promise<void> {
    return this.authenticate('/auth/telegram', payload);
  }

  logout(): void {
    localStorage.removeItem(JWT_KEY);
    this.token.set(null);
    this.user.set(null);
  }

  authHeader(): Record<string, string> {
    const token = this.token();
    return token ? { Authorization: 'Bearer ' + token } : {};
  }

  ensureUser(): Promise<Me | null> {
    if (this.user()) return Promise.resolve(this.user());
    if (!this.token()) return Promise.resolve(null);
    return this.loadMe();
  }

  tokens(): Promise<TokenView[]> {
    return this.request<TokenView[]>('GET', '/tokens');
  }

  createToken(name: string): Promise<CreatedToken> {
    return this.request<CreatedToken>('POST', '/tokens', { name });
  }

  deleteToken(id: string): Promise<void> {
    return this.request<void>('DELETE', `/tokens/${id}`);
  }

  usage(days: number): Promise<Usage> {
    return this.request<Usage>('GET', `/usage?days=${days}`);
  }

  profile(): Promise<ProfileView> {
    return this.request<ProfileView>('GET', '/profile');
  }

  updateProfile(firstName: string, lastName: string, userName: string): Promise<ProfileView> {
    return this.request<ProfileView>('PUT', '/profile', { firstName, lastName, userName });
  }

  setPassword(currentPassword: string | null, newPassword: string): Promise<void> {
    return this.request<void>('POST', '/profile/password', { currentPassword, newPassword });
  }

  unlinkLogin(provider: string): Promise<void> {
    return this.request<void>('DELETE', `/profile/logins/${provider}`);
  }

  async linkProvider(provider: string): Promise<void> {
    const res = await this.request<{ token: string }>('POST', '/profile/link-token');
    location.href = `${BASE}/auth/${provider}?link=${res.token}`;
  }

  tokenUsage(): Promise<TokenUsage[]> {
    return this.request<TokenUsage[]>('GET', '/usage/tokens');
  }

  providers(): Promise<ProviderInfo> {
    return this.request<ProviderInfo>('GET', '/auth/providers');
  }

  telegramLinks(): Promise<TelegramLinkView[]> {
    return this.request<TelegramLinkView[]>('GET', '/telegram/links');
  }

  connectTelegram(code: string): Promise<void> {
    return this.request<void>('POST', '/telegram/connect', { code });
  }

  createLinkToken(): Promise<LinkToken> {
    return this.request<LinkToken>('POST', '/telegram/link-token');
  }

  unlinkTelegram(telegramUserId: number): Promise<void> {
    return this.request<void>('DELETE', `/telegram/links/${telegramUserId}`);
  }

  adminUsage(): Promise<UsageRow[]> {
    return this.request<UsageRow[]>('GET', '/admin/usage');
  }

  adminUsers(): Promise<AdminUser[]> {
    return this.request<AdminUser[]>('GET', '/admin/users');
  }

  adminSetLimits(
    id: string,
    fileSizeLimitMb: number | null,
    dailyRequestLimit: number | null,
    concurrentLimit: number | null,
    expiresAt: string | null,
  ): Promise<Limits> {
    return this.request<Limits>('PUT', `/admin/users/${id}/limits`, {
      fileSizeLimitMb,
      dailyRequestLimit,
      concurrentLimit,
      expiresAt,
    });
  }

  refreshUser(): void {
    this.loadMe();
  }

  displayName(): string {
    const u = this.user();
    const full = [u?.firstName, u?.lastName].filter(Boolean).join(' ');
    return full || u?.email || '';
  }

  async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    let res: Response;
    try {
      res = await fetch(BASE + path, {
        method,
        headers: {
          ...(body ? { 'Content-Type': 'application/json' } : {}),
          ...this.authHeader(),
        },
        body: body ? JSON.stringify(body) : undefined,
      });
    } catch {
      throw { code: 'network', message: '' } satisfies ApiError;
    }
    if (res.status === 401) this.logout();
    if (res.ok) {
      const text = await res.text();
      return (text ? JSON.parse(text) : null) as T;
    }

    let data: { code?: string; detail?: string; message?: string } | null = null;
    try {
      data = await res.json();
    } catch {
      data = null;
    }
    throw {
      code: data?.code ?? (res.status === 401 ? 'unauthorized' : 'unknown'),
      message: data?.detail ?? data?.message ?? '',
    } satisfies ApiError;
  }

  private async authenticate(path: string, body: unknown): Promise<void> {
    const res = await this.request<{ token: string }>('POST', path, body);
    this.store(res.token);
    await this.loadMe();
  }

  private store(token: string): void {
    localStorage.setItem(JWT_KEY, token);
    this.token.set(token);
  }

  private async loadMe(): Promise<Me | null> {
    try {
      const me = await this.request<Me>('GET', '/me');
      this.user.set(me);
      return me;
    } catch {
      this.user.set(null);
      return null;
    }
  }
}
