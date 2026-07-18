export type MediaKind = 'video' | 'audio' | 'image' | 'gallery' | 'playlist';
export type OutputKind = 'audio' | 'video' | 'gallery';
export type Delivery = 'remux' | 'reencode';
export type JobState = 'queued' | 'running' | 'completed' | 'failed' | 'canceled' | 'expired';

export interface PlannedMp3 {
  qscale: number;
  typicalKbps: number;
}

export interface AudioVariant {
  formatId: string;
  codec: string;
  abrKbps: number | null;
  approxSizeBytes: number | null;
  plannedMp3: PlannedMp3;
}

export interface VideoVariant {
  formatId: string;
  codec: string;
  width: number | null;
  height: number | null;
  fps: number | null;
  vbrKbps: number | null;
  approxSizeBytes: number | null;
  needsMerge: boolean;
  delivery: Delivery;
}

export interface ImageEntry {
  index: number;
  thumbnailUrl: string | null;
  width: number | null;
  height: number | null;
}

export interface ProbeResult {
  url: string;
  kind: MediaKind;
  title: string;
  uploader: string | null;
  thumbnailUrl: string | null;
  durationSec: number | null;
  isLive: boolean;
  videoVariants: VideoVariant[];
  audioVariants: AudioVariant[];
  images: ImageEntry[];
}

export interface JobFile {
  url: string;
  fileName: string;
  sizeBytes: number;
  contentType: string;
  expiresAtUtc: string;
}

export interface JobStatus {
  jobId: string;
  state: JobState;
  phase: string | null;
  progressPct: number;
  etaSec: number | null;
  error: ApiError | null;
  file: JobFile | null;
}

export interface CreateJobResponse {
  jobId: string;
  statusUrl: string;
}

export interface HealthResponse {
  ok: boolean;
  version: string;
  ytdlp: string | null;
}

export interface ApiError {
  code: string;
  message: string;
}
