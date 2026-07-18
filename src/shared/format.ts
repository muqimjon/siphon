function pad(n: number): string {
  return n < 10 ? '0' + n : '' + n;
}

function clock(sec: number): string {
  const s = Math.max(0, Math.round(sec));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const r = s % 60;
  return h > 0 ? `${h}:${pad(m)}:${pad(r)}` : `${m}:${pad(r)}`;
}

export function bytes(n: number | null | undefined): string {
  if (n == null) return '—';
  if (n < 1024) return n + ' B';
  const units = ['KB', 'MB', 'GB', 'TB'];
  let i = -1;
  let v = n;
  do {
    v /= 1024;
    i++;
  } while (v >= 1024 && i < units.length - 1);
  return (v >= 100 ? Math.round(v) : v.toFixed(1)) + ' ' + units[i];
}

export function kbps(n: number | null | undefined): string {
  return n == null ? '—' : Math.round(n) + ' kb/s';
}

export function eta(sec: number | null | undefined): string {
  return sec == null ? '--:--' : clock(sec);
}

export function duration(sec: number | null | undefined): string {
  return sec == null ? '—' : clock(sec);
}
