import { applyI18n, setLang, t } from "../i18n";
import { send } from "../messages";
import { createFlow, type Flow } from "../content/flow";

document.documentElement.dataset.theme = matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";

const input = document.getElementById("url-input") as HTMLInputElement;
const result = document.getElementById("result") as HTMLElement;
let flow: Flow;

async function main(): Promise<void> {
  const r = await send({ type: "settings.get" });
  setLang(r.settings.lang);
  applyI18n(document);
  flow = createFlow({
    container: result,
    settings: r.settings,
    onStart: () => result.classList.remove("hidden"),
    onDone: loadRecents
  });
  init();
}

function init(): void {
  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    const u = tabs[0]?.url;
    if (u && /^https?:/.test(u) && !input.value) input.value = u;
  });
  document.getElementById("paste-btn")!.addEventListener("click", () => {
    navigator.clipboard.readText().then((v) => {
      input.value = v.trim();
      input.focus();
    }).catch(() => input.focus());
  });
  document.getElementById("probe-form")!.addEventListener("submit", (e) => {
    e.preventDefault();
    const u = input.value.trim();
    if (u) flow.start(u);
  });
  document.getElementById("options-btn")!.addEventListener("click", () => {
    chrome.runtime.openOptionsPage();
  });
  loadRecents();
  checkHealth();
}

function loadRecents(): void {
  send({ type: "recents.get" }).then((r) => {
    const box = document.getElementById("recents-box")!;
    const list = document.getElementById("recents")!;
    list.textContent = "";
    box.classList.toggle("hidden", !r.recents.length);
    r.recents.forEach((rec) => {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "recent";
      const badge = document.createElement("span");
      badge.className = "sp-badge";
      badge.textContent = rec.output;
      const title = document.createElement("span");
      title.className = "recent-title";
      title.textContent = rec.title || rec.fileName || rec.url;
      b.appendChild(badge);
      b.appendChild(title);
      b.addEventListener("click", () => {
        input.value = rec.url;
        flow.start(rec.url);
      });
      list.appendChild(b);
    });
  }).catch(() => { });
}

function checkHealth(): void {
  const dot = document.getElementById("health-dot")!;
  const text = document.getElementById("health-text")!;
  send({ type: "health.check" }).then((r) => {
    const ok = r.health && r.health.ok;
    dot.className = "dot " + (ok ? "ok" : "err");
    text.textContent = ok ? t("health.ok", { version: r.health.version }) : t("health.offline");
  }).catch(() => {
    dot.className = "dot err";
    text.textContent = t("health.offline");
  });
}

main();
