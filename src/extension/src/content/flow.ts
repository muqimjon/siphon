import type { OutputKind, ProbeResult } from "@shared/models";
import { bytes, eta } from "@shared/format";
import { el } from "../dom";
import { t } from "../i18n";
import { send, type Job, type PortMessage, type Settings } from "../messages";
import { renderVariants } from "./variants";

const CHECK = '<svg class="sp-check" viewBox="0 0 52 52"><circle cx="26" cy="26" r="24"/><path d="M15 27l7 7 14-14"/></svg>';
const LIMIT = 2 * 1024 * 1024 * 1024;

type ErrLike = { code: string; message?: string };

export interface FlowOptions {
  container: HTMLElement;
  settings: Settings;
  onStart?: () => void;
  onLive?: () => void;
  onDone?: (job: Job) => void;
}

export interface Flow {
  start(url: string): void;
  cancel(): void;
  probing(): boolean;
  destroy(): void;
}

interface Progress {
  bar: HTMLElement;
  fill: HTMLElement;
  label: HTMLElement;
  right: HTMLElement;
}

export function createFlow(opts: FlowOptions): Flow {
  const container = opts.container;
  const settings = opts.settings;
  let probe: ProbeResult | null = null;
  let url = "";
  let probeId: string | null = null;
  let jobId: string | null = null;
  let port: chrome.runtime.Port | null = null;
  let lastPick: { output: OutputKind; formatId: string | null } | null = null;
  let prog: Progress | null = null;

  function connect(): void {
    if (port) return;
    port = chrome.runtime.connect({ name: "siphon-ui" });
    port.onMessage.addListener(onPort);
    port.onDisconnect.addListener(() => {
      port = null;
      if (jobId) setTimeout(connect, 500);
    });
  }

  function onPort(msg: PortMessage): void {
    let j: Job | undefined;
    if (msg.type === "jobs.snapshot") j = msg.jobs.find((x) => x.jobId === jobId);
    else if (msg.type === "job.update" && msg.job.jobId === jobId) j = msg.job;
    if (j) renderJob(j);
  }

  function start(u: string): void {
    cancelProbe();
    url = u;
    jobId = null;
    probe = null;
    lastPick = null;
    renderSkeleton();
    const id = (probeId = Math.random().toString(36).slice(2));
    send({ type: "probe", url: u, probeId: id }).then((r) => {
      if (id !== probeId) return;
      probeId = null;
      probe = r.probe;
      if (probe.isLive) return renderLive();
      if (settings.behavior === "auto") return autoPick();
      renderVariantsView();
    }, (e: ErrLike) => {
      if (id !== probeId) return;
      probeId = null;
      if (e.code === "canceled") return;
      renderError(e, () => start(u));
    });
    opts.onStart?.();
  }

  function cancelProbe(): void {
    if (!probeId) return;
    send({ type: "probe.cancel", probeId }).catch(() => { });
    probeId = null;
  }

  function autoPick(): void {
    const v = (probe!.videoVariants || [])[0];
    const a = (probe!.audioVariants || [])[0];
    if (v) return pick("video", v.formatId);
    if (a) return pick("audio", a.formatId);
    if ((probe!.images || []).length) return pick("gallery", null);
    renderError({ code: "unavailable" }, () => start(url));
  }

  function variantSize(output: OutputKind, formatId: string | null): number | null {
    const list = output === "video" ? probe!.videoVariants : output === "audio" ? probe!.audioVariants : null;
    const m = (list || []).find((x) => x.formatId === formatId);
    return m ? m.approxSizeBytes : null;
  }

  function pick(output: OutputKind, formatId: string | null): void {
    lastPick = { output, formatId };
    const size = variantSize(output, formatId);
    if (settings.warnLarge && size != null && size > LIMIT) return renderWarnLarge(size, output, formatId);
    createJob(output, formatId);
  }

  function createJob(output: OutputKind, formatId: string | null): void {
    renderProgress();
    connect();
    send({ type: "job.create", url, output, formatId, title: probe?.title }).then((r) => {
      jobId = r.jobId;
    }, (e: ErrLike) => {
      renderError(e, () => createJob(output, formatId));
    });
  }

  function renderSkeleton(): void {
    container.textContent = "";
    const box = el("div", "sp-variants");
    ["48px", "44px", "44px"].forEach((h) => {
      const s = el("div", "sp-skeleton");
      s.style.height = h;
      box.appendChild(s);
    });
    container.appendChild(box);
  }

  function renderVariantsView(): void {
    renderVariants(container, probe!, { onPick: pick, busy: false });
  }

  function renderWarnLarge(size: number, output: OutputKind, formatId: string | null): void {
    container.textContent = "";
    const card = el("div", "sp-warn-card");
    card.appendChild(el("strong", null, t("ext.warnLarge", { size: bytes(size) })));
    const row = el("div", "sp-actions");
    const ok = el("button", "sp-btn sp-btn-primary", t("ext.continue"));
    ok.type = "button";
    ok.addEventListener("click", () => createJob(output, formatId));
    const back = el("button", "sp-btn sp-btn-secondary", t("ext.cancel"));
    back.type = "button";
    back.addEventListener("click", renderVariantsView);
    row.appendChild(ok);
    row.appendChild(back);
    card.appendChild(row);
    container.appendChild(card);
  }

  function renderProgress(): void {
    container.textContent = "";
    const box = el("div", "sp-flow-progress");
    if (probe) box.appendChild(el("div", "sp-probe-title", probe.title));
    const bar = el("div", "sp-progress indeterminate");
    const fill = el("div", "sp-progress-fill");
    bar.appendChild(fill);
    const meta = el("div", "sp-flow-meta");
    const label = el("span", null, t("state.queued"));
    const right = el("span", null, "");
    meta.appendChild(label);
    meta.appendChild(right);
    box.appendChild(bar);
    box.appendChild(meta);
    container.appendChild(box);
    prog = { bar, fill, label, right };
  }

  function renderJob(j: Job): void {
    if (j.stage === "job" || j.stage === "handoff") {
      if (!prog || !container.contains(prog.bar)) renderProgress();
      const p = prog!;
      if (j.stage === "handoff") {
        p.bar.classList.add("indeterminate");
        p.fill.style.width = "100%";
        p.label.textContent = t("ext.handoff");
        p.right.textContent = j.fileName || "";
      } else if (j.state === "running") {
        if (j.progressPct > 0) {
          p.bar.classList.remove("indeterminate");
          p.fill.style.width = j.progressPct + "%";
          p.right.textContent = Math.round(j.progressPct) + "% · " + eta(j.etaSec);
        } else {
          p.bar.classList.add("indeterminate");
          p.right.textContent = "";
        }
        p.label.textContent = t("phase." + (j.phase || "downloading"));
      } else {
        p.bar.classList.add("indeterminate");
        p.label.textContent = t("state.queued");
        p.right.textContent = "";
      }
    } else if (j.stage === "done") {
      renderDone(j);
    } else if (j.stage === "error") {
      renderError(j.error || { code: "unknown" }, () => {
        if (lastPick) createJob(lastPick.output, lastPick.formatId);
        else start(url);
      });
    }
  }

  function renderDone(j: Job): void {
    container.textContent = "";
    const box = el("div", "sp-done");
    box.innerHTML = CHECK;
    box.appendChild(el("div", "sp-probe-title", t("done.title")));
    if (j.fileName) box.appendChild(el("div", "sp-flow-file", j.fileName));
    const row = el("div", "sp-actions");
    const show = el("button", "sp-btn sp-btn-primary", t("ext.showInFolder"));
    show.type = "button";
    show.addEventListener("click", () => {
      if (j.downloadId != null) send({ type: "download.show", downloadId: j.downloadId }).catch(() => { });
    });
    const again = el("button", "sp-btn sp-btn-secondary", t("ext.again"));
    again.type = "button";
    again.addEventListener("click", () => {
      if (probe) renderVariantsView();
      else start(url);
    });
    row.appendChild(show);
    row.appendChild(again);
    box.appendChild(row);
    container.appendChild(box);
    opts.onDone?.(j);
  }

  function renderLive(): void {
    renderMessage(t("err.live-not-supported"), null);
    opts.onLive?.();
  }

  function renderMessage(text: string, retry: (() => void) | null): void {
    container.textContent = "";
    const card = el("div", "sp-error-card");
    card.appendChild(el("strong", null, text));
    if (retry) {
      const b = el("button", "sp-btn sp-btn-secondary", t("retry"));
      b.type = "button";
      b.addEventListener("click", retry);
      card.appendChild(b);
    }
    container.appendChild(card);
  }

  function renderError(e: ErrLike, retry: (() => void) | null): void {
    let msg = t("err." + e.code);
    if (msg === "err." + e.code) msg = t("err.unknown");
    renderMessage(msg, retry);
  }

  return {
    start,
    cancel: cancelProbe,
    probing: () => !!probeId,
    destroy() {
      cancelProbe();
      jobId = null;
      if (port) {
        port.disconnect();
        port = null;
      }
    }
  };
}
