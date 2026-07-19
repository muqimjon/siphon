import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Auth, Usage } from '../../services/auth';
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

  readonly usage = signal<Usage | null>(null);
  readonly bars = signal<Bar[]>([]);

  constructor() {
    this.auth.usage(14).then((u) => this.applyUsage(u)).catch(() => {});
  }

  fmtDate(iso: string | null): string {
    if (!iso) return this.i18n.t('dash.never');
    return new Date(iso).toLocaleDateString(this.i18n.lang());
  }

  volume(bytes: number): string {
    const gb = bytes / 1073741824;
    return gb >= 1 ? gb.toFixed(1) : (bytes / 1048576).toFixed(0) + ' MB';
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
