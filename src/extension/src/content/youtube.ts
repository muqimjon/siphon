import { mount, type PanelHandle } from "./panel";

const DROP = '<svg viewBox="0 0 24 24" width="24" height="24"><path d="M12 2C12 2 5 10.5 5 15a7 7 0 0 0 14 0C19 10.5 12 2 12 2Z" fill="#22D3EE"/></svg>';

let handle: PanelHandle | null = null;
let shortsBtn: HTMLButtonElement | null = null;
let observer: MutationObserver | null = null;
let obsTimer = 0;

function theme(): string {
  return document.documentElement.hasAttribute("dark") ? "dark" : "light";
}

new MutationObserver(() => {
  if (handle) handle.host.dataset.theme = theme();
}).observe(document.documentElement, { attributes: true, attributeFilter: ["dark"] });

function stopWaiting(): void {
  if (observer) {
    observer.disconnect();
    observer = null;
  }
  if (obsTimer) {
    clearTimeout(obsTimer);
    obsTimer = 0;
  }
}

function unmount(): void {
  stopWaiting();
  if (handle) {
    const h = handle;
    handle = null;
    h.destroy();
  }
  if (shortsBtn) {
    shortsBtn.remove();
    shortsBtn = null;
  }
}

function waitFor<T>(find: () => T | null, cb: (found: T) => void): void {
  const found = find();
  if (found) return cb(found);
  observer = new MutationObserver(() => {
    const f = find();
    if (!f) return;
    stopWaiting();
    cb(f);
  });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  obsTimer = setTimeout(stopWaiting, 10000) as unknown as number;
}

function findAnchor(): { el: Element; pos: "first" | "after" } | null {
  const m = document.querySelector("ytd-watch-metadata #title");
  if (m) return { el: m, pos: "after" };
  const a = document.querySelector("#above-the-fold #title");
  if (a) return { el: a, pos: "after" };
  const b = document.querySelector("#below");
  if (b) return { el: b, pos: "first" };
  return null;
}

function findRail(): Element | null {
  return document.querySelector("ytd-reel-video-renderer[is-active] #actions")
    || document.querySelector("#actions.ytd-reel-player-overlay-renderer");
}

function makeShortsButton(url: string): HTMLButtonElement {
  const b = document.createElement("button");
  b.type = "button";
  b.setAttribute("aria-label", "Siphon");
  b.style.cssText = "width:48px;height:48px;border-radius:50%;border:0;cursor:pointer;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.4);margin:8px auto;";
  b.innerHTML = DROP;
  b.addEventListener("click", () => {
    if (handle) return;
    handle = mount({
      mode: "modal",
      url,
      theme: theme(),
      onDestroy: () => { handle = null; }
    });
  });
  return b;
}

function route(): void {
  unmount();
  const p = location.pathname;
  if (p === "/watch") {
    const v = new URLSearchParams(location.search).get("v");
    if (!v) return;
    const url = "https://www.youtube.com/watch?v=" + v;
    waitFor(findAnchor, (a) => {
      handle = mount({ mode: "inline", anchor: a.el, pos: a.pos, url, theme: theme() });
    });
  } else if (p.indexOf("/shorts/") === 0) {
    const id = p.split("/")[2];
    if (!id) return;
    const watchUrl = "https://www.youtube.com/watch?v=" + id;
    waitFor(findRail, (rail) => {
      shortsBtn = makeShortsButton(watchUrl);
      rail.appendChild(shortsBtn);
    });
  }
}

document.addEventListener("yt-navigate-finish", route);
route();
