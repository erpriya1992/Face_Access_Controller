import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, EmployeeSettingsRow } from '../../api.service';

type AccessFilter = 'all' | 'allowed' | 'blocked';

@Component({
  selector: 'app-settings',
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent implements OnInit {
  employees: EmployeeSettingsRow[] = [];
  searchText = '';
  accessFilter: AccessFilter = 'all';
  loading = false;
  message = '';
  savingPersonId: string | null = null;

  constructor(
    private api: ApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadEmployees();
  }

  loadEmployees(): void {
    this.loading = true;
    this.message = '';
    this.api.getEmployeesForSettings().subscribe({
      next: (rows) => {
        this.employees = (rows ?? []).map((r) => this.normalizeRow(r));
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.message = 'Could not load enrolled users. Check your connection and try again.';
        this.cdr.markForCheck();
      }
    });
  }

  private normalizeRow(raw: EmployeeSettingsRow | Record<string, unknown>): EmployeeSettingsRow {
    const r = raw as Record<string, unknown>;
    const pick = (c: string, p: string) => r[c] ?? r[p];
    const created = pick('createdAt', 'CreatedAt');
    const fdId = pick('faceDeviceId', 'FaceDeviceId');
    return {
      id: Number(pick('id', 'Id') ?? 0),
      personId: String(pick('personId', 'PersonId') ?? '').trim(),
      fullName: String(pick('fullName', 'FullName') ?? ''),
      department: (pick('department', 'Department') as string | null | undefined) ?? null,
      phone: (pick('phone', 'Phone') as string | null | undefined) ?? null,
      idCardNumber: (pick('idCardNumber', 'IdCardNumber') as string | null | undefined) ?? null,
      doorAccessAllowed: Boolean(pick('doorAccessAllowed', 'DoorAccessAllowed')),
      createdAt: created == null ? '' : typeof created === 'string' ? created : String(created),
      faceDeviceId: fdId == null || fdId === '' ? null : Number(fdId),
      faceDeviceName: (pick('faceDeviceName', 'FaceDeviceName') as string | null | undefined) ?? null
    };
  }

  filteredRows(): EmployeeSettingsRow[] {
    let list = this.employees;
    const q = this.searchText.trim().toLowerCase();
    if (q) {
      list = list.filter(
        (e) =>
          e.personId.toLowerCase().includes(q) ||
          e.fullName.toLowerCase().includes(q) ||
          (e.department ?? '').toLowerCase().includes(q) ||
          (e.faceDeviceName ?? '').toLowerCase().includes(q)
      );
    }
    if (this.accessFilter === 'allowed') {
      list = list.filter((e) => e.doorAccessAllowed);
    } else if (this.accessFilter === 'blocked') {
      list = list.filter((e) => !e.doorAccessAllowed);
    }
    return list;
  }

  counts(): { total: number; allowed: number; blocked: number } {
    const total = this.employees.length;
    const allowed = this.employees.filter((e) => e.doorAccessAllowed).length;
    return { total, allowed, blocked: total - allowed };
  }

  setAccess(row: EmployeeSettingsRow, allowed: boolean): void {
    if (row.doorAccessAllowed === allowed) {
      return;
    }
    const id = row.personId;
    this.savingPersonId = id;
    this.message = '';
    this.api.updateEmployeeDoorAccess(id, allowed).subscribe({
      next: (res) => {
        this.savingPersonId = null;
        const local = this.employees.find((e) => e.personId === id);
        if (local) {
          local.doorAccessAllowed = res.doorAccessAllowed;
        }
        if (res.warning) {
          this.message = res.warning;
        } else if (res.syncedToDevice) {
          this.message = `Access updated for ${id} and synced to the terminal.`;
        } else {
          this.message = `Access preference saved for ${id}.`;
        }
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.savingPersonId = null;
        const payload = err?.error;
        const msg =
          payload && typeof payload === 'object' && typeof (payload as { message?: string }).message === 'string'
            ? (payload as { message: string }).message
            : 'Update failed.';
        this.message = msg;
        this.cdr.markForCheck();
      }
    });
  }

  formatCreated(iso: string): string {
    if (!iso) {
      return '—';
    }
    try {
      return new Date(iso).toLocaleString();
    } catch {
      return iso;
    }
  }
}
