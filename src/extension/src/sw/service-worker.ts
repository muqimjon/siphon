import type { ApiError } from "@shared/models";
import type { Message, MessageType, OkResponse } from "../messages";
import * as api from "./api";
import * as jobs from "./jobs";

const probes = new Map<string, AbortController>();

chrome.alarms.create("siphon-jobs", { periodInMinutes: 0.5 });

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (alarm.name !== "siphon-jobs") return;
  await jobs.ready;
  jobs.resume();
});

chrome.runtime.onConnect.addListener(async (port) => {
  if (port.name !== "siphon-ui") return;
  await jobs.ready;
  jobs.addPort(port);
});

chrome.downloads.onChanged.addListener(async (delta) => {
  await jobs.ready;
  jobs.onDownloadChanged(delta);
});

type Handlers = { [K in MessageType]: (msg: Message<K>) => Promise<OkResponse<K>> | OkResponse<K> };

const handlers: Handlers = {
  async probe(msg) {
    const ctl = new AbortController();
    probes.set(msg.probeId, ctl);
    try {
      const cookies = await api.youtubeCookies(msg.url);
      const probe = await api.probe(msg.url, cookies, ctl.signal);
      return { ok: true, probe };
    } finally {
      probes.delete(msg.probeId);
    }
  },
  "probe.cancel"(msg) {
    probes.get(msg.probeId)?.abort();
    return { ok: true };
  },
  async "job.create"(msg) {
    const cookies = await api.youtubeCookies(msg.url);
    const r = await api.createJob(msg.url, msg.output, msg.formatId, cookies);
    await jobs.ready;
    jobs.track(r.jobId, { url: msg.url, title: msg.title, output: msg.output, formatId: msg.formatId });
    return { ok: true, jobId: r.jobId };
  },
  async "settings.get"() {
    return { ok: true, settings: await api.getSettings() };
  },
  async "settings.set"(msg) {
    await api.setSettings(msg.patch);
    return { ok: true };
  },
  async "recents.get"() {
    await jobs.ready;
    return { ok: true, recents: await jobs.getRecents() };
  },
  async "health.check"(msg) {
    return { ok: true, health: await api.health(msg.url) };
  },
  "download.show"(msg) {
    chrome.downloads.show(msg.downloadId);
    return { ok: true };
  }
};

chrome.runtime.onMessage.addListener((msg: Message, _sender, sendResponse) => {
  const handler = handlers[msg?.type] as ((m: Message) => Promise<OkResponse<MessageType>> | OkResponse<MessageType>) | undefined;
  if (!handler) return;
  Promise.resolve(handler(msg)).then(sendResponse, (e: ApiError) =>
    sendResponse({ ok: false, error: { code: e?.code || "unknown", message: e?.message || "" } }));
  return true;
});
