import { applyI18n, setLang, t } from "../i18n";
import { send, type Settings } from "../messages";

document.documentElement.dataset.theme = matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";

const backend = document.getElementById("backend") as HTMLInputElement;
const apiKey = document.getElementById("api-key") as HTMLInputElement;
const cookies = document.getElementById("cookies") as HTMLInputElement;
const warn = document.getElementById("warn-large") as HTMLInputElement;
let lang = "";

async function main(): Promise<void> {
  const r = await send({ type: "settings.get" });
  const s = r.settings;
  backend.value = s.backendUrl;
  apiKey.value = s.apiKey;
  cookies.checked = s.cookies;
  warn.checked = s.warnLarge;
  const radio = document.querySelector<HTMLInputElement>(`input[name="behavior"][value="${s.behavior}"]`);
  if (radio) radio.checked = true;
  lang = s.lang;
  setLang(lang);
  markLang();
  applyI18n(document);
}

function markLang(): void {
  document.querySelectorAll<HTMLElement>("#lang-seg button").forEach((b) => {
    b.classList.toggle("active", b.dataset.lang === lang);
  });
}

document.querySelectorAll<HTMLElement>("#lang-seg button").forEach((b) => {
  b.addEventListener("click", () => {
    lang = b.dataset.lang!;
    send({ type: "settings.set", patch: { lang: lang as Settings["lang"] } }).then(() => {
      setLang(lang);
      markLang();
      applyI18n(document);
    });
  });
});

document.getElementById("reveal")!.addEventListener("click", () => {
  apiKey.type = apiKey.type === "password" ? "text" : "password";
});

function originPattern(u: string): string | null {
  try {
    const x = new URL(u);
    if (!/^https?:$/.test(x.protocol)) return null;
    if (x.protocol === "http:" && /^(localhost|127\.0\.0\.1)$/.test(x.hostname)) return null;
    return x.protocol + "//" + x.hostname + "/*";
  } catch {
    return null;
  }
}

function ensureOrigin(u: string): Promise<unknown> {
  const o = originPattern(u);
  if (!o) return Promise.resolve();
  return chrome.permissions.request({ origins: [o] }).catch(() => { });
}

document.getElementById("test")!.addEventListener("click", () => {
  const u = backend.value.trim().replace(/\/+$/, "");
  const res = document.getElementById("test-result")!;
  res.className = "hint";
  res.textContent = "...";
  ensureOrigin(u).then(() => send({ type: "health.check", url: u })).then((r) => {
    const ok = r.health && r.health.ok;
    res.className = "hint " + (ok ? "ok" : "err");
    res.textContent = ok ? t("health.ok", { version: r.health.version }) : t("health.offline");
  }).catch(() => {
    res.className = "hint err";
    res.textContent = t("health.offline");
  });
});

cookies.addEventListener("change", () => {
  if (cookies.checked) {
    chrome.permissions.request({ permissions: ["cookies"] }).then((granted) => {
      if (!granted) {
        cookies.checked = false;
        return;
      }
      send({ type: "settings.set", patch: { cookies: true } });
    });
  } else {
    chrome.permissions.remove({ permissions: ["cookies"] });
    send({ type: "settings.set", patch: { cookies: false } });
  }
});

document.getElementById("save")!.addEventListener("click", () => {
  const u = backend.value.trim().replace(/\/+$/, "") || "http://localhost:5046";
  ensureOrigin(u).then(() => {
    const checked = document.querySelector<HTMLInputElement>('input[name="behavior"]:checked');
    return send({
      type: "settings.set",
      patch: {
        backendUrl: u,
        apiKey: apiKey.value.trim(),
        behavior: (checked ? checked.value : "ask") as Settings["behavior"],
        warnLarge: warn.checked,
        cookies: cookies.checked
      }
    });
  }).then(() => {
    const saved = document.getElementById("saved")!;
    saved.textContent = t("opt.saved");
    setTimeout(() => { saved.textContent = ""; }, 2000);
  });
});

main();
