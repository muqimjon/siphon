import type { OutputKind, ProbeResult } from "@shared/models";
import { bytes, duration, kbps } from "@shared/format";
import { el } from "../dom";
import { t } from "../i18n";

const DL_ICON = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 4v11m0 0l-4.5-4.5M12 15l4.5-4.5M5 20h14"/></svg>';

interface VariantsOptions {
  onPick: (output: OutputKind, formatId: string | null) => void;
  busy: boolean;
}

function sizeText(n: number | null): string {
  return n == null ? "—" : "~" + bytes(n);
}

function section(title: string): { root: HTMLDivElement; body: HTMLDivElement } {
  const root = el("div", "sp-section");
  const head = el("button", "sp-section-head", title);
  head.type = "button";
  head.setAttribute("aria-expanded", "false");
  const collapse = el("div", "sp-collapse");
  const body = el("div", "sp-section-body");
  collapse.appendChild(body);
  head.addEventListener("click", () => {
    const open = collapse.classList.toggle("open");
    head.classList.toggle("open", open);
    head.setAttribute("aria-expanded", String(open));
  });
  root.appendChild(head);
  root.appendChild(collapse);
  return { root, body };
}

export function renderVariants(container: HTMLElement, probe: ProbeResult, opts: VariantsOptions): void {
  const buttons: HTMLButtonElement[] = [];
  container.textContent = "";
  const root = el("div", "sp-variants");

  const head = el("div", "sp-probe-head");
  if (probe.thumbnailUrl) {
    const img = el("img", "sp-thumb");
    img.src = probe.thumbnailUrl;
    img.alt = "";
    head.appendChild(img);
  }
  const meta = el("div", "sp-probe-meta");
  meta.appendChild(el("div", "sp-probe-title", probe.title));
  const sub = [probe.uploader, probe.durationSec != null ? duration(probe.durationSec) : null]
    .filter(Boolean).join(" · ");
  if (sub) meta.appendChild(el("div", "sp-probe-sub", sub));
  head.appendChild(meta);
  root.appendChild(head);

  const audios = probe.audioVariants || [];
  const videos = probe.videoVariants || [];
  const images = probe.images || [];

  const smart = el("div", "sp-smart-row");
  if (audios.length) {
    const a = audios[0];
    const ab = el("button", "sp-btn sp-btn-primary sp-smart");
    ab.type = "button";
    ab.appendChild(el("span", null, t("variants.audio")));
    ab.appendChild(el("small", null, t("variants.audioSub", {
      codec: a.codec, abr: kbps(a.abrKbps), mp3: a.plannedMp3.typicalKbps
    })));
    ab.addEventListener("click", () => opts.onPick("audio", a.formatId));
    buttons.push(ab);
    smart.appendChild(ab);
  }
  if (videos.length) {
    const v = videos[0];
    const vb = el("button", "sp-btn sp-btn-primary sp-smart");
    vb.type = "button";
    vb.appendChild(el("span", null, t("variants.video")));
    vb.appendChild(el("small", null, t("variants.videoSub", {
      res: v.height != null ? v.height + "p" : v.codec,
      codec: v.codec, size: sizeText(v.approxSizeBytes)
    })));
    vb.addEventListener("click", () => opts.onPick("video", v.formatId));
    buttons.push(vb);
    smart.appendChild(vb);
  }
  if (smart.children.length) root.appendChild(smart);

  if (images.length) {
    const gb = el("button", "sp-btn sp-btn-primary sp-gallery");
    gb.type = "button";
    gb.textContent = t("variants.galleryAll", { count: images.length });
    gb.addEventListener("click", () => opts.onPick("gallery", null));
    buttons.push(gb);
    root.appendChild(gb);
  }

  if (audios.length) {
    const as = section(t("variants.audioSection"));
    audios.forEach((a) => {
      const row = el("div", "sp-row");
      row.appendChild(el("span", "sp-badge", a.codec));
      row.appendChild(el("span", "sp-row-main", kbps(a.abrKbps) + " → MP3 ~" + a.plannedMp3.typicalKbps + "k"));
      row.appendChild(el("span", "sp-row-size", a.approxSizeBytes == null ? "—" : bytes(a.approxSizeBytes)));
      const btn = el("button", "sp-icon-btn");
      btn.type = "button";
      btn.setAttribute("aria-label", t("variants.download"));
      btn.innerHTML = DL_ICON;
      btn.addEventListener("click", () => opts.onPick("audio", a.formatId));
      buttons.push(btn);
      row.appendChild(btn);
      as.body.appendChild(row);
    });
    root.appendChild(as.root);
  }

  if (videos.length) {
    const vs = section(t("variants.videoSection"));
    videos.forEach((v) => {
      const row = el("div", "sp-row");
      row.appendChild(el("span", "sp-badge", v.codec));
      const label = v.height != null
        ? v.height + "p" + (v.fps != null && v.fps > 30 ? Math.round(v.fps) : "")
        : kbps(v.vbrKbps);
      row.appendChild(el("span", "sp-row-main", String(label)));
      if (v.delivery === "reencode") row.appendChild(el("span", "sp-badge sp-badge-warn", t("variants.reencode")));
      row.appendChild(el("span", "sp-row-size", v.approxSizeBytes == null ? "—" : bytes(v.approxSizeBytes)));
      const btn = el("button", "sp-icon-btn");
      btn.type = "button";
      btn.setAttribute("aria-label", t("variants.download"));
      btn.innerHTML = DL_ICON;
      btn.addEventListener("click", () => opts.onPick("video", v.formatId));
      buttons.push(btn);
      row.appendChild(btn);
      vs.body.appendChild(row);
    });
    root.appendChild(vs.root);
  }

  if (opts.busy) buttons.forEach((b) => { b.disabled = true; });
  container.appendChild(root);
}
