import { Injectable } from '@angular/core';
import {
  ApiError,
  CreateJobResponse,
  HealthResponse,
  JobStatus,
  OutputKind,
  ProbeResult,
} from '@shared/models';

const BASE = '/api/v1';

function apiKey(): string {
  return localStorage.getItem('siphon.apiKey') || 'dev-web-key';
}

@Injectable({ providedIn: 'root' })
export class Api {
  probe(url: string): Promise<ProbeResult> {
    return this.request<ProbeResult>('POST', '/probe', { url });
  }

  createJob(url: string, output: OutputKind, formatId: string | null): Promise<CreateJobResponse> {
    return this.request<CreateJobResponse>('POST', '/jobs', { url, output, formatId });
  }

  job(id: string): Promise<JobStatus> {
    return this.request<JobStatus>('GET', `/jobs/${id}`);
  }

  async health(): Promise<HealthResponse> {
    const res = await fetch(`${BASE}/health`);
    return res.json();
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    let res: Response;
    try {
      res = await fetch(BASE + path, {
        method,
        headers: { 'Content-Type': 'application/json', 'X-Api-Key': apiKey() },
        body: body ? JSON.stringify(body) : undefined,
      });
    } catch {
      throw { code: 'network', message: '' } satisfies ApiError;
    }
    if (res.ok) return res.status === 204 ? (null as T) : res.json();

    let data: { code?: string; detail?: string; message?: string } | null = null;
    try {
      data = await res.json();
    } catch {
      data = null;
    }
    throw {
      code: data?.code ?? (res.status === 401 ? 'unauthorized' : 'unknown'),
      message: data?.detail ?? data?.message ?? '',
    } satisfies ApiError;
  }
}
