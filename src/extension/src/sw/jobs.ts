import type { ApiError, JobFile } from "@shared/models";
import type { Job, PortMessage, Recent } from "../messages";
import { getSettings, jobStatus } from "./api";

const jobs = new Map<string, Job>();
const ports = new Set<chrome.runtime.Port>();
let timer = 0;
let ticking = false;

export const ready = (async () => {
  const data = await chrome.storage.session.get("jobs");
  ((data.jobs as Job[] | undefined) || []).forEach((j) => jobs.set(j.jobId, j));
  resume();
})();

export function resume(): void {
  if ([...jobs.values()].some((j) => j.stage === "job")) startPolling();
}

function save(): Promise<void> {
  return chrome.storage.session.set({ jobs: [...jobs.values()] });
}

export function addPort(port: chrome.runtime.Port): void {
  ports.add(port);
  port.onDisconnect.addListener(() => ports.delete(port));
  post(port, { type: "jobs.snapshot", jobs: [...jobs.values()] });
}

function post(port: chrome.runtime.Port, msg: PortMessage): void {
  try { port.postMessage(msg); } catch { }
}

function broadcast(job: Job): void {
  for (const p of ports) post(p, { type: "job.update", job });
}

export function track(jobId: string, meta: Pick<Job, "url" | "title" | "output" | "formatId">): Job {
  for (const [id, j] of [...jobs]) {
    if (j.stage !== "job" && Date.now() - (j.at || 0) > 1800000) jobs.delete(id);
  }
  const job: Job = { jobId, stage: "job", state: "queued", progressPct: 0, at: Date.now(), ...meta };
  jobs.set(jobId, job);
  save();
  startPolling();
  broadcast(job);
  return job;
}

function startPolling(): void {
  if (!timer) timer = setInterval(tick, 1000) as unknown as number;
}

async function tick(): Promise<void> {
  if (ticking) return;
  ticking = true;
  const act = [...jobs.values()].filter((j) => j.stage === "job");
  if (!act.length) {
    clearInterval(timer);
    timer = 0;
    ticking = false;
    return;
  }
  await Promise.all(act.map(poll));
  await save();
  ticking = false;
}

async function poll(job: Job): Promise<void> {
  let s;
  try {
    s = await jobStatus(job.jobId);
  } catch (e) {
    const err = e as ApiError;
    if (err.code === "network" && (job.fails = (job.fails || 0) + 1) < 5) return;
    return fail(job, err.code ? err : { code: "network", message: "" });
  }
  job.fails = 0;
  job.state = s.state;
  job.phase = s.phase;
  job.progressPct = s.progressPct;
  job.etaSec = s.etaSec;
  if (s.state === "completed") return startDownload(job, s.file!);
  if (s.state === "failed") return fail(job, s.error || { code: "unknown", message: "" });
  if (s.state === "expired") return fail(job, { code: "token-expired", message: "" });
  if (s.state === "canceled") return fail(job, { code: "unknown", message: "" });
  broadcast(job);
}

async function startDownload(job: Job, file: JobFile): Promise<void> {
  job.stage = "handoff";
  job.fileName = file.fileName;
  broadcast(job);
  const s = await getSettings();
  const url = s.backendUrl.replace(/\/+$/, "") + file.url;
  try {
    job.downloadId = await chrome.downloads.download({ url, filename: file.fileName, conflictAction: "uniquify" });
  } catch {
    try {
      job.downloadId = await chrome.downloads.download({ url, conflictAction: "uniquify" });
    } catch {
      return fail(job, { code: "unknown", message: "" });
    }
  }
  await save();
  broadcast(job);
}

export async function onDownloadChanged(delta: chrome.downloads.DownloadDelta): Promise<void> {
  const job = [...jobs.values()].find((j) => j.downloadId === delta.id);
  if (!job || !delta.state) return;
  if (delta.state.current === "complete") {
    job.stage = "done";
    await addRecent(job);
    await save();
    broadcast(job);
  } else if (delta.state.current === "interrupted") {
    const reason = delta.error?.current;
    if (reason === "USER_CANCELED" || reason === "FILE_BLOCKED" || reason === "FILE_ACCESS_DENIED")
      return fail(job, { code: "canceled", message: "" });
    if (job.retried) return fail(job, { code: "network", message: "" });
    job.retried = true;
    try {
      const s = await jobStatus(job.jobId);
      if (s.state !== "completed" || !s.file) throw s;
      await startDownload(job, s.file);
    } catch {
      fail(job, { code: "token-expired", message: "" });
    }
  }
}

function fail(job: Job, error: ApiError): void {
  job.stage = "error";
  job.error = error;
  save();
  broadcast(job);
}

async function addRecent(job: Job): Promise<void> {
  const data = await chrome.storage.local.get("recents");
  const recents = (data.recents as Recent[] | undefined) || [];
  recents.unshift({
    url: job.url,
    title: job.title,
    output: job.output,
    formatId: job.formatId,
    fileName: job.fileName,
    at: Date.now()
  });
  await chrome.storage.local.set({ recents: recents.slice(0, 20) });
}

export async function getRecents(): Promise<Recent[]> {
  const data = await chrome.storage.local.get("recents");
  return ((data.recents as Recent[] | undefined) || []).slice(0, 5);
}
