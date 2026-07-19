import type { ApiError, CreateJobResponse, HealthResponse, JobStatus, ProbeResult } from "@shared/models";
import type { Settings } from "../messages";

const DEFAULTS: Settings = {
  backendUrl: "http://localhost:5046",
  apiKey: "dev-extension-key",
  lang: "uz",
  behavior: "ask",
  cookies: false,
  warnLarge: true
};

export function getSettings(): Promise<Settings> {
  return chrome.storage.local.get(DEFAULTS) as Promise<Settings>;
}

export function setSettings(patch: Partial<Settings>): Promise<void> {
  return chrome.storage.local.set(patch);
}

function base(url: string): string {
  return url.replace(/\/+$/, "");
}

interface RequestOpts {
  backendUrl?: string;
  signal?: AbortSignal;
}

async function request<T>(method: string, path: string, body?: unknown, opts: RequestOpts = {}): Promise<T> {
  const s = await getSettings();
  let res: Response;
  try {
    res = await fetch(base(opts.backendUrl || s.backendUrl) + path, {
      method,
      headers: { "Content-Type": "application/json", "X-Api-Key": s.apiKey },
      body: body ? JSON.stringify(body) : undefined,
      signal: opts.signal
    });
  } catch (e) {
    throw { code: (e as DOMException)?.name === "AbortError" ? "canceled" : "network", message: "" } as ApiError;
  }
  if (res.ok) return res.status === 204 ? (null as T) : res.json();
  let data: { code?: string; detail?: string; message?: string } | null = null;
  try { data = await res.json(); } catch { }
  throw {
    code: data?.code || (res.status === 401 ? "unauthorized" : "unknown"),
    message: data?.detail || data?.message || ""
  } as ApiError;
}

export function probe(url: string, cookies: string | undefined, signal: AbortSignal): Promise<ProbeResult> {
  return request("POST", "/api/v1/probe", { url, cookies }, { signal });
}

export function createJob(url: string, output: string, formatId: string | null, cookies: string | undefined): Promise<CreateJobResponse> {
  return request("POST", "/api/v1/jobs", { url, output, formatId, cookies });
}

export function jobStatus(id: string): Promise<JobStatus> {
  return request("GET", "/api/v1/jobs/" + id);
}

export async function health(url?: string): Promise<HealthResponse> {
  const s = await getSettings();
  const res = await fetch(base(url || s.backendUrl) + "/api/v1/health");
  return res.json();
}

export async function youtubeCookies(url: string): Promise<string | undefined> {
  const s = await getSettings();
  if (!s.cookies) return undefined;
  let host: string;
  try { host = new URL(url).hostname; } catch { return undefined; }
  if (!/(^|\.)(youtube\.com|youtu\.be)$/.test(host)) return undefined;
  const granted = await chrome.permissions.contains({ permissions: ["cookies"] });
  if (!granted) return undefined;
  const all = await chrome.cookies.getAll({ domain: "youtube.com" });
  if (!all.length) return undefined;
  const lines = ["# Netscape HTTP Cookie File"];
  for (const c of all) {
    lines.push([
      c.domain,
      c.domain.startsWith(".") ? "TRUE" : "FALSE",
      c.path,
      c.secure ? "TRUE" : "FALSE",
      Math.floor(c.expirationDate || 0),
      c.name,
      c.value
    ].join("\t"));
  }
  return lines.join("\n");
}
