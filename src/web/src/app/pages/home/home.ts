import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { bytes, eta } from '@shared/format';
import { ApiError, JobFile, JobStatus, OutputKind, ProbeResult } from '@shared/models';
import { Api } from '../../services/api';
import { I18n } from '../../services/i18n';
import { Variants } from '../../variants/variants';

type Stage = 'idle' | 'probing' | 'variants' | 'progress' | 'done' | 'error';

interface Progress {
  state: 'queued' | 'running';
  pct: number;
  phase: string | null;
  etaSec: number | null;
}

@Component({
  selector: 'sp-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Variants, RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly api = inject(Api);
  readonly i18n = inject(I18n);
  readonly bytes = bytes;

  readonly stage = signal<Stage>('idle');
  readonly url = signal('');
  readonly probe = signal<ProbeResult | null>(null);
  readonly progress = signal<Progress | null>(null);
  readonly doneFile = signal<JobFile | null>(null);
  readonly errorKey = signal<string | null>(null);

  readonly sites = ['YouTube', 'Instagram', 'TikTok', 'X', 'Facebook', 'SoundCloud', 'Vimeo', 'Reddit'];

  private readonly downloadLink = viewChild<ElementRef<HTMLAnchorElement>>('downloadLink');
  private pollToken = 0;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private retryAction: () => void = () => {};
  private autoClicked = false;

  readonly indeterminate = computed(() => {
    const p = this.progress();
    return !p || p.state === 'queued' || p.pct <= 0;
  });
  readonly fillPct = computed(() => Math.max(0, this.progress()?.pct ?? 0));
  readonly progressLabel = computed(() => {
    const p = this.progress();
    if (!p || p.state === 'queued') return this.i18n.t('state.queued');
    return this.i18n.t('phase.' + (p.phase || 'downloading'));
  });
  readonly progressRight = computed(() => {
    const p = this.progress();
    if (!p || p.state !== 'running' || p.pct <= 0) return '';
    return `${Math.round(p.pct)}% · ${eta(p.etaSec)}`;
  });
  readonly errText = computed(() => {
    const key = this.errorKey();
    if (!key) return '';
    const text = this.i18n.t(key);
    return text === key && key.startsWith('err.') ? this.i18n.t('err.unknown') : text;
  });

  constructor() {
    effect(() => {
      const link = this.downloadLink();
      if (link && this.stage() === 'done' && !this.autoClicked) {
        this.autoClicked = true;
        link.nativeElement.click();
      }
    });
  }

  submit(event: Event): void {
    event.preventDefault();
    this.startProbe();
  }

  paste(input: HTMLInputElement): void {
    if (!navigator.clipboard?.readText) {
      input.focus();
      return;
    }
    navigator.clipboard
      .readText()
      .then((value) => {
        this.url.set(value.trim());
        input.focus();
      })
      .catch(() => {});
  }

  onPick(pick: { output: OutputKind; formatId: string | null }): void {
    const url = this.probe()!.url;
    this.autoClicked = false;
    this.startProgress();
    this.api
      .createJob(url, pick.output, pick.formatId)
      .then((res) => this.poll(res.jobId, () => this.onPick(pick)))
      .catch((err: ApiError) => this.fail(err.code, () => this.onPick(pick)));
  }

  retry(): void {
    this.retryAction();
  }

  private startProbe(): void {
    const url = this.url().trim();
    if (!url) return;
    this.stopPoll();
    this.stage.set('probing');
    this.api
      .probe(url)
      .then((result) => {
        this.probe.set(result);
        this.stage.set('variants');
      })
      .catch((err: ApiError) => this.fail(err.code, () => this.startProbe()));
  }

  private startProgress(): void {
    this.progress.set({ state: 'queued', pct: 0, phase: null, etaSec: null });
    this.stage.set('progress');
  }

  private poll(id: string, retry: () => void): void {
    this.stopPoll();
    const token = this.pollToken;
    let fails = 0;
    this.pollTimer = setInterval(() => {
      this.api
        .job(id)
        .then((status) => {
          if (this.pollToken !== token) return;
          fails = 0;
          this.applyStatus(status, retry);
        })
        .catch(() => {
          if (this.pollToken !== token) return;
          if (++fails >= 5) {
            this.stopPoll();
            this.fail('network', () => {
              this.startProgress();
              this.poll(id, retry);
            });
          }
        });
    }, 2000);
  }

  private applyStatus(status: JobStatus, retry: () => void): void {
    switch (status.state) {
      case 'queued':
        this.progress.set({ state: 'queued', pct: 0, phase: null, etaSec: null });
        return;
      case 'running':
        this.progress.set({ state: 'running', pct: status.progressPct, phase: status.phase, etaSec: status.etaSec });
        return;
      case 'completed':
        this.stopPoll();
        this.doneFile.set(status.file!);
        this.stage.set('done');
        return;
      case 'failed':
        this.stopPoll();
        this.fail(status.error?.code ?? 'unknown', retry);
        return;
      default:
        this.stopPoll();
        this.setError(status.state === 'expired' ? 'state.expired' : 'state.canceled', retry);
    }
  }

  private fail(code: string, retry: () => void): void {
    this.setError('err.' + code, retry);
  }

  private setError(key: string, retry: () => void): void {
    this.retryAction = retry;
    this.errorKey.set(key);
    this.stage.set('error');
  }

  private stopPoll(): void {
    this.pollToken++;
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
