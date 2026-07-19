import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { bytes, duration, kbps } from '@shared/format';
import { AudioVariant, OutputKind, ProbeResult, VideoVariant } from '@shared/models';
import { I18n } from '../services/i18n';

export interface Pick {
  output: OutputKind;
  formatId: string | null;
  format: string | null;
}

@Component({
  selector: 'sp-variants',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './variants.html',
})
export class Variants {
  readonly i18n = inject(I18n);
  readonly bytes = bytes;
  readonly kbps = kbps;

  readonly probe = input.required<ProbeResult>();
  readonly busy = input(false);
  readonly pick = output<Pick>();

  readonly audios = computed(() =>
    [...(this.probe().audioVariants ?? [])].sort((a, b) => (b.abrKbps ?? 0) - (a.abrKbps ?? 0)),
  );
  readonly videos = computed(() =>
    [...(this.probe().videoVariants ?? [])].sort(
      (a, b) => (b.height ?? 0) - (a.height ?? 0) || (b.fps ?? 0) - (a.fps ?? 0) || (b.vbrKbps ?? 0) - (a.vbrKbps ?? 0),
    ),
  );
  readonly images = computed(() => this.probe().images ?? []);

  readonly audioFormats = computed(() => this.probe().audioFormats ?? []);
  readonly videoFormats = computed(() => this.probe().videoFormats ?? []);

  readonly audioFormat = signal('best');
  readonly videoFormat = signal('mp4');

  readonly subtitle = computed(() => {
    const p = this.probe();
    return [p.uploader, p.durationSec != null ? duration(p.durationSec) : null].filter(Boolean).join(' · ');
  });

  readonly audioOpen = signal(false);
  readonly videoOpen = signal(false);

  formatLabel(format: string): string {
    return format === 'best' ? this.i18n.t('variants.formatBest') : format.toUpperCase();
  }

  audioDetail(a: AudioVariant): string {
    return this.audioFormat() === 'mp3' ? `${kbps(a.abrKbps)} → MP3 ~${a.plannedMp3.typicalKbps}k` : kbps(a.abrKbps);
  }

  smartSize(value: number | null): string {
    return value == null ? '—' : '~' + bytes(value);
  }

  videoRes(v: VideoVariant): string {
    return v.height != null ? v.height + 'p' : v.codec;
  }

  videoLabel(v: VideoVariant): string {
    if (v.height == null) return kbps(v.vbrKbps);
    const fps = v.fps != null && v.fps > 30 ? Math.round(v.fps) : '';
    return `${v.height}p${fps}`;
  }

  emit(output: OutputKind, formatId: string | null, format: string | null): void {
    this.pick.emit({ output, formatId, format });
  }
}
