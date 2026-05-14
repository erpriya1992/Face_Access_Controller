import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../api.service';

@Component({
  selector: 'app-live',
  imports: [CommonModule, FormsModule],
  templateUrl: './live.component.html',
  styleUrl: './live.component.scss'
})
export class LiveComponent implements OnInit {
  transactions: any[] = [];
  message = '';
  startDateTime = '';
  endDateTime = '';
  deviceStatus: 'Online' | 'Offline' | 'Checking' = 'Checking';
  lastDeviceCheck = '';
  autoSyncSeconds = 5;
  private autoSyncTimer?: ReturnType<typeof setInterval>;
  private autoRefreshTimer?: ReturnType<typeof setInterval>;

  constructor(private api: ApiService) {}

  async ngOnInit(): Promise<void> {
    try {
      await this.api.connectLive(() => {
        this.deviceStatus = 'Online';
        this.loadTransactions();
      });
    } catch {
      // Keep polling fallback active even if SignalR is unavailable.
      this.message = 'Live socket unavailable. Using auto polling mode.';
    }

    this.loadTransactions();
    this.syncTransactions();

    this.autoSyncTimer = setInterval(() => {
      this.syncTransactions();
    }, this.autoSyncSeconds * 1000);

    // Always refresh visible grid periodically even if sync fails.
    this.autoRefreshTimer = setInterval(() => {
      this.loadTransactions();
    }, this.autoSyncSeconds * 1000);
  }

  syncTransactions(isManual = false): void {
    this.deviceStatus = 'Checking';
    this.api.syncTransactions().subscribe({
      next: (res) => {
        this.deviceStatus = 'Online';
        this.lastDeviceCheck = new Date().toLocaleString();
        if (isManual) {
          this.message = `${res.synced} new records synced.`;
        }
        this.loadTransactions();
      },
      error: () => {
        this.deviceStatus = 'Offline';
        this.lastDeviceCheck = new Date().toLocaleString();
        this.message = 'Device offline or sync failed.';
      }
    });
  }

  loadTransactions(): void {
    this.api.getLiveTransactions(this.startDateTime, this.endDateTime).subscribe({
      next: (res) => (this.transactions = res),
      error: () => (this.message = 'Unable to load transactions.')
    });
  }

  ngOnDestroy(): void {
    if (this.autoSyncTimer) {
      clearInterval(this.autoSyncTimer);
    }
    if (this.autoRefreshTimer) {
      clearInterval(this.autoRefreshTimer);
    }
  }
}
