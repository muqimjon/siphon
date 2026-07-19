import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AdminUser, Auth, UsageRow } from '../../services/auth';
import { I18n } from '../../services/i18n';

interface Bar {
  date: string;
  label: string;
  count: number;
  pct: number;
}

@Component({
  selector: 'sp-admin',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin.html',
  styleUrl: './admin.css',
})
export class Admin {
  readonly i18n = inject(I18n);
  private readonly auth = inject(Auth);

  readonly users = signal<AdminUser[]>([]);
  readonly bars = signal<Bar[]>([]);
  readonly total = signal(0);
  readonly edited = signal<AdminUser | null>(null);
  readonly concurrentVal = signal('');
  readonly expiresVal = signal('');
  readonly fileVal = signal('');
  readonly dailyVal = signal('');
  readonly saving = signal(false);

  constructor() {
    this.auth.adminUsers().then((users) => this.users.set(users)).catch(() => {});
    this.auth.adminUsage().then((rows) => this.buildChart(rows)).catch(() => {});
  }

  fmtDate(iso: string): string {
    return new Date(iso).toLocaleDateString(this.i18n.lang());
  }

  startEdit(u: AdminUser): void {
    this.fileVal.set(u.limits.fileSizeLimitMbOverride?.toString() ?? '');
    this.dailyVal.set(u.limits.dailyRequestLimitOverride?.toString() ?? '');
    this.concurrentVal.set(u.limits.concurrentLimitOverride?.toString() ?? '');
    this.expiresVal.set(u.limits.overridesExpireAt ? u.limits.overridesExpireAt.slice(0, 10) : '');
    this.edited.set(u);
  }

  cancelEdit(): void {
    this.edited.set(null);
  }

  grantMonths(months: number): void {
    const d = new Date();
    d.setMonth(d.getMonth() + months);
    this.expiresVal.set(d.toISOString().slice(0, 10));
  }

  clearExpiry(): void {
    this.expiresVal.set('');
  }

  save(): void {
    const u = this.edited();
    if (!u) return;
    this.saving.set(true);
    const expires = this.expiresVal().trim();
    this.auth
      .adminSetLimits(
        u.id,
        this.parse(this.fileVal()),
        this.parse(this.dailyVal()),
        this.parse(this.concurrentVal()),
        expires ? new Date(expires + 'T23:59:59Z').toISOString() : null,
      )
      .then((limits) => {
        this.users.update((rows) => rows.map((r) => (r.id === u.id ? { ...r, limits } : r)));
        this.edited.set(null);
      })
      .catch(() => {})
      .finally(() => this.saving.set(false));
  }

  userLabel(u: AdminUser): string {
    const full = [u.firstName, u.lastName].filter(Boolean).join(' ');
    return full || u.email || u.id;
  }

  private parse(value: string): number | null {
    const text = value.trim();
    if (!text) return null;
    const n = Math.floor(Number(text));
    return Number.isFinite(n) && n > 0 ? n : null;
  }

  private buildChart(rows: UsageRow[]): void {
    const byDay = new Map<string, number>();
    for (const row of rows) byDay.set(row.dateUtc, (byDay.get(row.dateUtc) ?? 0) + row.count);

    const now = new Date();
    const days: Bar[] = [];
    for (let i = 13; i >= 0; i--) {
      const d = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - i));
      const date = d.toISOString().slice(0, 10);
      days.push({ date, label: String(d.getUTCDate()), count: byDay.get(date) ?? 0, pct: 0 });
    }
    const max = Math.max(1, ...days.map((d) => d.count));
    this.bars.set(days.map((d) => ({ ...d, pct: Math.round((d.count / max) * 100) })));
    this.total.set(days.reduce((sum, d) => sum + d.count, 0));
  }
}
