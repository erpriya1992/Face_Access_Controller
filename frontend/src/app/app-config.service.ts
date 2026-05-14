import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export type AppConfig = {
  apiBaseUrl: string;
  hubUrl: string;
};

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private config?: AppConfig;

  constructor(private http: HttpClient) {}

  load(): Promise<void> {
    return firstValueFrom(this.http.get<AppConfig>('/assets/app-config.json'))
      .then((cfg) => {
        this.config = cfg;
      })
      .catch(() => {
        // Keep config undefined; ApiService will fall back to its current defaults.
      });
  }

  get apiBaseUrl(): string {
    return this.config?.apiBaseUrl ?? 'https://localhost:7298/api';
  }

  get hubUrl(): string {
    return this.config?.hubUrl ?? 'https://localhost:7298/hubs/live-transactions';
  }
}

