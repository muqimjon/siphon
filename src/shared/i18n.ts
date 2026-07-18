import uz from './i18n/uz.json';
import ru from './i18n/ru.json';
import en from './i18n/en.json';

export type Lang = 'uz' | 'ru' | 'en';
export type Dict = Record<string, string>;

export const DICTS: Record<Lang, Dict> = { uz, ru, en };
export const LANGS: Lang[] = ['uz', 'ru', 'en'];
export const DEFAULT_LANG: Lang = 'uz';

export function isLang(value: string | null | undefined): value is Lang {
  return value === 'uz' || value === 'ru' || value === 'en';
}

export function resolveLang(value: string | null | undefined): Lang {
  if (isLang(value)) return value;
  const base = (value ?? '').slice(0, 2).toLowerCase();
  return isLang(base) ? base : DEFAULT_LANG;
}

export function translate(lang: Lang, key: string, params?: Record<string, string | number>): string {
  let text = DICTS[lang][key] ?? DICTS.en[key] ?? key;
  if (params) {
    for (const name of Object.keys(params)) {
      text = text.split('{' + name + '}').join(String(params[name]));
    }
  }
  return text;
}
