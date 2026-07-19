import tokensCss from "@shared/styles/tokens.css";
import componentsCss from "@shared/styles/components.css";
import { el } from "../dom";
import { setLang, t } from "../i18n";
import { send } from "../messages";
import { createFlow, type Flow } from "./flow";

const DROP = '<svg class="drop" viewBox="0 0 24 24"><path d="M12 2C12 2 5 10.5 5 15a7 7 0 0 0 14 0C19 10.5 12 2 12 2Z" fill="currentColor"/></svg>';
const CHEV = '<svg class="chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9l6 6 6-6"/></svg>';

const PANEL_CSS = [
  ":host { all: initial; display: block; }",
  "*, *::before, *::after { box-sizing: border-box; }",
  ".wrap { font-family: var(--sp-font-yt); color: var(--sp-text); }",
  ".pill { display: inline-flex; align-items: center; gap: var(--sp-2); padding: 7px 14px; border: 1px solid var(--sp-border); border-radius: var(--sp-r-pill); background: var(--sp-surface); color: var(--sp-text); font-family: var(--sp-font-yt); font-size: var(--sp-fs-md); font-weight: 600; cursor: pointer; transition: filter var(--sp-t-fast) var(--sp-ease); }",
  ".pill:hover { filter: brightness(1.1); }",
  ".pill:disabled { opacity: .55; cursor: default; }",
  ".pill .drop { width: 15px; height: 15px; color: var(--sp-accent); }",
  ".pill .chev { width: 14px; height: 14px; transition: transform var(--sp-t-med) var(--sp-ease); }",
  ".pill.open .chev { transform: rotate(180deg); }",
  ".card { margin-top: var(--sp-2); }",
  ".card-inner { background: var(--sp-surface); border: 1px solid var(--sp-border); border-radius: var(--sp-r-lg); padding: var(--sp-4); box-shadow: var(--sp-shadow); max-width: 560px; }",
  ".overlay { position: fixed; inset: 0; z-index: 2147483000; background: rgba(0,0,0,.55); display: flex; align-items: center; justify-content: center; }",
  ".modal { width: min(420px, calc(100vw - 32px)); max-height: 80vh; overflow: auto; }",
  ".modal-head { display: flex; align-items: center; gap: var(--sp-2); font-weight: 700; font-size: var(--sp-fs-lg); margin-bottom: var(--sp-3); }",
  ".modal-head .drop { width: 18px; height: 18px; color: var(--sp-accent); }",
  ".close { margin-left: auto; width: 28px; height: 28px; border: 0; border-radius: var(--sp-r-sm); background: var(--sp-surface-2); color: var(--sp-text); font-size: 16px; line-height: 1; cursor: pointer; }"
].join("\n");

let sheets: CSSStyleSheet[] | null = null;

function getSheets(): CSSStyleSheet[] {
  if (!sheets) {
    sheets = [tokensCss, componentsCss, PANEL_CSS].map((css) => {
      const s = new CSSStyleSheet();
      s.replaceSync(css);
      return s;
    });
  }
  return sheets;
}

export interface MountOptions {
  mode: "modal" | "inline";
  url: string;
  theme?: string;
  anchor?: Element;
  pos?: "first" | "after";
  onDestroy?: () => void;
}

export interface PanelHandle {
  host: HTMLDivElement;
  destroy(): void;
}

export function mount(opts: MountOptions): PanelHandle {
  const host = document.createElement("div");
  host.dataset.theme = opts.theme || "dark";
  const root = host.attachShadow({ mode: "closed" });
  root.adoptedStyleSheets = getSheets();
  const wrap = el("div", "wrap");
  root.appendChild(wrap);
  let flow: Flow | null = null;
  let pill: HTMLButtonElement | null = null;
  let collapse: HTMLDivElement | null = null;

  function destroy(): void {
    if (flow) flow.destroy();
    host.remove();
    opts.onDestroy?.();
  }

  function startFlow(body: HTMLElement): void {
    send({ type: "settings.get" }).then((r) => {
      setLang(r.settings.lang);
      flow = createFlow({
        container: body,
        settings: r.settings,
        onLive() {
          if (!pill) return;
          collapse!.classList.remove("open");
          pill.classList.remove("open");
          pill.disabled = true;
          pill.title = t("err.live-not-supported");
        }
      });
      flow.start(opts.url);
    }).catch(() => { });
  }

  if (opts.mode === "modal") {
    const overlay = el("div", "overlay");
    const card = el("div", "card-inner modal");
    const head = el("div", "modal-head");
    head.innerHTML = DROP;
    head.appendChild(el("span", null, "Siphon"));
    const close = el("button", "close", "×");
    close.type = "button";
    close.addEventListener("click", destroy);
    head.appendChild(close);
    const body = el("div");
    card.appendChild(head);
    card.appendChild(body);
    overlay.appendChild(card);
    overlay.addEventListener("click", (e) => {
      if (e.target === overlay) destroy();
    });
    wrap.appendChild(overlay);
    document.body.appendChild(host);
    startFlow(body);
  } else {
    pill = el("button", "pill");
    pill.type = "button";
    pill.innerHTML = DROP + "<span>Siphon</span>" + CHEV;
    collapse = el("div", "card sp-collapse");
    const inner = el("div");
    const panelBody = el("div", "card-inner");
    inner.appendChild(panelBody);
    collapse.appendChild(inner);
    let probed = false;
    pill.addEventListener("click", () => {
      const open = collapse!.classList.toggle("open");
      pill!.classList.toggle("open", open);
      if (open && !probed) {
        probed = true;
        startFlow(panelBody);
      }
      if (!open && flow) {
        if (flow.probing()) probed = false;
        flow.cancel();
      }
    });
    wrap.appendChild(pill);
    wrap.appendChild(collapse);
    if (opts.pos === "first") opts.anchor!.insertBefore(host, opts.anchor!.firstChild);
    else opts.anchor!.insertAdjacentElement("afterend", host);
  }

  return { host, destroy };
}
