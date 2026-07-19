import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { I18n } from '../../services/i18n';

@Component({
  selector: 'sp-api-docs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './docs.html',
  styleUrl: './docs.css',
})
export class ApiDocs {
  readonly i18n = inject(I18n);
  readonly copiedKey = signal<string | null>(null);

  readonly baseUrl = 'https://your-host/api/v1';
  readonly authHeader = 'Authorization: Bearer sk_live_...';

  readonly probeCurl =
    `curl -X POST ${this.baseUrl}/probe \\\n` +
    `  -H "Authorization: Bearer sk_live_..." \\\n` +
    `  -H "Content-Type: application/json" \\\n` +
    `  -d '{"url":"https://youtu.be/dQw4w9WgXcQ"}'`;
  readonly probeJs =
    `const res = await fetch("${this.baseUrl}/probe", {\n` +
    `  method: "POST",\n` +
    `  headers: {\n` +
    `    "Authorization": "Bearer sk_live_...",\n` +
    `    "Content-Type": "application/json"\n` +
    `  },\n` +
    `  body: JSON.stringify({ url: "https://youtu.be/dQw4w9WgXcQ" })\n` +
    `});\n` +
    `const probe = await res.json();`;
  readonly probeRes =
    `{\n` +
    `  "title": "Never Gonna Give You Up",\n` +
    `  "kind": "video",\n` +
    `  "audioVariants": [ { "formatId": "140", "codec": "aac", "abrKbps": 128 } ],\n` +
    `  "videoVariants": [ { "formatId": "137", "codec": "avc1", "height": 1080 } ],\n` +
    `  "images": []\n` +
    `}`;

  readonly jobsCurl =
    `curl -X POST ${this.baseUrl}/jobs \\\n` +
    `  -H "Authorization: Bearer sk_live_..." \\\n` +
    `  -H "Content-Type: application/json" \\\n` +
    `  -d '{"url":"https://youtu.be/dQw4w9WgXcQ","output":"audio","format":"mp3"}'`;
  readonly jobsRes = `{ "jobId": "job_a1b2c3" }`;

  readonly statusCurl =
    `curl ${this.baseUrl}/jobs/job_a1b2c3 \\\n` +
    `  -H "Authorization: Bearer sk_live_..."`;
  readonly statusRes =
    `{\n` +
    `  "state": "completed",\n` +
    `  "progressPct": 100,\n` +
    `  "file": {\n` +
    `    "url": "/api/v1/files/file_x9?token=sk_tok_...",\n` +
    `    "fileName": "never-gonna-give-you-up.mp3",\n` +
    `    "sizeBytes": 4128572\n` +
    `  }\n` +
    `}`;

  readonly filesCurl =
    `curl -L "${this.baseUrl}/files/file_x9?token=sk_tok_..." \\\n` +
    `  -o audio.mp3`;

  readonly errorCodes = [
    { code: 'invalid-url', key: 'err.invalid-url' },
    { code: 'unsupported-site', key: 'err.unsupported-site' },
    { code: 'unavailable', key: 'err.unavailable' },
    { code: 'login-required', key: 'err.login-required' },
    { code: 'too-large', key: 'err.too-large' },
    { code: 'quota-exceeded', key: 'docs.errQuota' },
    { code: 'unauthorized', key: 'err.unauthorized' },
    { code: 'job-not-found', key: 'err.job-not-found' },
  ];

  copy(key: string, text: string): void {
    if (!navigator.clipboard?.writeText) return;
    navigator.clipboard
      .writeText(text)
      .then(() => {
        this.copiedKey.set(key);
        setTimeout(() => this.copiedKey.set(null), 1500);
      })
      .catch(() => {});
  }
}
