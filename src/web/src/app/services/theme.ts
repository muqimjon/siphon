import { effect, Injectable, signal } from '@angular/core';

type Mode = 'dark' | 'light';
const KEY = 'siphon.theme';

function initial(): Mode {
  const saved = localStorage.getItem(KEY);
  if (saved === 'dark' || saved === 'light') return saved;
  return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

@Injectable({ providedIn: 'root' })
export class Theme {
  readonly mode = signal<Mode>(initial());

  constructor() {
    effect(() => {
      const mode = this.mode();
      localStorage.setItem(KEY, mode);
      document.documentElement.dataset['theme'] = mode;
    });
  }

  toggle(): void {
    this.mode.update((m) => (m === 'dark' ? 'light' : 'dark'));
  }
}
