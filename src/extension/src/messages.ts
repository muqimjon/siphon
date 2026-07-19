import type { ApiError, HealthResponse, JobState, OutputKind, ProbeResult } from "@shared/models";
import type { Lang } from "@shared/i18n";

export type JobStage = "job" | "handoff" | "done" | "error";

export interface Job {
  jobId: string;
  stage: JobStage;
  state: JobState;
  progressPct: number;
  at: number;
  url: string;
  title?: string;
  output: OutputKind;
  formatId: string | null;
  phase?: string | null;
  etaSec?: number | null;
  fails?: number;
  fileName?: string;
  downloadId?: number;
  retried?: boolean;
  error?: ApiError;
}

export type PortMessage =
  | { type: "jobs.snapshot"; jobs: Job[] }
  | { type: "job.update"; job: Job };

export interface Settings {
  backendUrl: string;
  apiKey: string;
  lang: Lang;
  behavior: "ask" | "auto";
  cookies: boolean;
  warnLarge: boolean;
}

export interface Recent {
  url: string;
  title?: string;
  output: OutputKind;
  formatId: string | null;
  fileName?: string;
  at: number;
}

export interface Requests {
  probe: { url: string; probeId: string };
  "probe.cancel": { probeId: string };
  "job.create": { url: string; output: OutputKind; formatId: string | null; title?: string };
  "settings.get": {};
  "settings.set": { patch: Partial<Settings> };
  "recents.get": {};
  "health.check": { url?: string };
  "download.show": { downloadId: number };
}

export interface Responses {
  probe: { probe: ProbeResult };
  "probe.cancel": {};
  "job.create": { jobId: string };
  "settings.get": { settings: Settings };
  "settings.set": {};
  "recents.get": { recents: Recent[] };
  "health.check": { health: HealthResponse };
  "download.show": {};
}

export type MessageType = keyof Requests;
export type Message<K extends MessageType = MessageType> = { type: K } & Requests[K];
export type OkResponse<K extends MessageType> = { ok: true } & Responses[K];
export type ErrResponse = { ok: false; error: ApiError };

export async function send<K extends MessageType>(msg: { type: K } & Requests[K]): Promise<OkResponse<K>> {
  const r = (await chrome.runtime.sendMessage(msg)) as OkResponse<K> | ErrResponse | undefined;
  if (!r) throw { code: "unknown", message: "" } as ApiError;
  if (!r.ok) throw r.error || { code: "unknown", message: "" };
  return r;
}
