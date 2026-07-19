import { resolveLang, translate, type Lang } from "@shared/i18n";

let lang: Lang = "en";

export function setLang(value: string | null | undefined): void {
  lang = resolveLang(value);
}

export function t(key: string, params?: Record<string, string | number>): string {
  return translate(lang, key, params);
}

export function applyI18n(root: Document | Element = document): void {
  root.querySelectorAll<HTMLElement>("[data-i18n]").forEach((el) => {
    el.textContent = t(el.getAttribute("data-i18n")!);
  });
  root.querySelectorAll<HTMLElement>("[data-i18n-attr]").forEach((el) => {
    el.getAttribute("data-i18n-attr")!.split(";").forEach((pair) => {
      const i = pair.indexOf(":");
      el.setAttribute(pair.slice(0, i), t(pair.slice(i + 1)));
    });
  });
}
