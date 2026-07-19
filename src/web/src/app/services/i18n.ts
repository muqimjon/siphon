import { effect, Injectable, signal } from '@angular/core';
import { Lang, LANGS, resolveLang, translate } from '@shared/i18n';

const KEY = 'siphon.lang';

@Injectable({ providedIn: 'root' })
export class I18n {
  readonly langs = LANGS;
  readonly lang = signal<Lang>(resolveLang(localStorage.getItem(KEY)));

  constructor() {
    effect(() => {
      const lang = this.lang();
      localStorage.setItem(KEY, lang);
      document.documentElement.lang = lang;
    });
  }

  t(key: string, params?: Record<string, string | number>): string {
    return translate(this.lang(), key, params);
  }

  set(lang: Lang): void {
    this.lang.set(lang);
  }
}
