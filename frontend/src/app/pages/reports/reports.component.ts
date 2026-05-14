import { CommonModule } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  ViewChild
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Chart, registerables } from 'chart.js';
import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';
import {
  ApiService,
  DailyVolumeItem,
  DepartmentAccessItem,
  HourlyAccessItem,
  PagedReport,
  TopScannerItem
} from '../../api.service';
import { exportToExcel, exportToPdf } from './report-export.util';

Chart.register(...registerables);

type ReportTab = 'daily' | 'monthly' | 'inout' | 'hours' | 'insights' | 'activity';

@Component({
  selector: 'app-reports',
  imports: [CommonModule, FormsModule],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss'
})
export class ReportsComponent implements OnInit, OnDestroy {
  activeTab: ReportTab = 'daily';
  searchText = '';

  today = new Date().toISOString().slice(0, 10);
  month = new Date().getMonth() + 1;
  year = new Date().getFullYear();

  inOutDate = new Date().toISOString().slice(0, 10);
  hoursYear = new Date().getFullYear();
  hoursMonth = new Date().getMonth() + 1;
  activityDate = new Date().toISOString().slice(0, 10);

  insightsDate = new Date().toISOString().slice(0, 10);
  insightsVolumeEndDate = new Date().toISOString().slice(0, 10);
  insightsVolumeDays = 14;
  insightsTopStart = '';
  insightsTopEnd = new Date().toISOString().slice(0, 10);

  insightsHourly: HourlyAccessItem[] = [];
  insightsVolume: DailyVolumeItem[] = [];
  insightsTop: TopScannerItem[] = [];
  insightsDept: DepartmentAccessItem[] = [];
  private insightsLoaded = false;

  private hourlyChart?: Chart;
  private volumeChart?: Chart;
  private topChart?: Chart;
  private deptChart?: Chart;

  @ViewChild('hourlyCanvas') hourlyCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('volumeCanvas') volumeCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('topCanvas') topCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('deptCanvas') deptCanvas?: ElementRef<HTMLCanvasElement>;

  dailyReport: DailyRow[] = [];
  monthlyReport: MonthlyRow[] = [];
  inOutReport: InOutRow[] = [];
  hoursReport: HoursRow[] = [];
  activityReport: ActivityRow[] = [];

  /** HR filter: only employees with 3+ gate reads (re-entry pattern). */
  showMultiAccessOnly = false;
  expandedPersonId: string | null = null;

  loading = false;
  message = '';
  exportBusy = false;
  /** Person ID currently being removed (disables that row's button). */
  deletingPersonId: string | null = null;

  /** Daily row shown in the centered face / employee details modal (click thumbnail). */
  faceDetailModal: DailyRow | null = null;

  /**
   * Coordinates overlapping report HTTP calls. An older response must not clear `loading` or
   * overwrite data after a newer request has started (e.g. switching tabs quickly).
   */
  private reportLoadSeq = 0;
  private dailyReqSeq = 0;
  private monthlyReqSeq = 0;
  private inOutReqSeq = 0;
  private hoursReqSeq = 0;
  private activityReqSeq = 0;
  private insightsReqSeq = 0;

  /** Shared page size for all paged reports (max 200 on server). */
  reportPageSize = 50;

  dailyPage = 1;
  dailyTotalCount = 0;
  dailyTotalPages = 1;

  monthlyPage = 1;
  monthlyTotalCount = 0;
  monthlyTotalPages = 1;

  inOutPage = 1;
  inOutTotalCount = 0;
  inOutTotalPages = 1;

  hoursPage = 1;
  hoursTotalCount = 0;
  hoursTotalPages = 1;

  activityPage = 1;
  activityTotalCount = 0;
  activityTotalPages = 1;

  constructor(
    private api: ApiService,
    private cdr: ChangeDetectorRef
  ) {}

  private startReportSequence(silent: boolean): number {
    const seq = ++this.reportLoadSeq;
    if (!silent) {
      this.loading = true;
    }
    return seq;
  }

  /** Clears the spinner when this HTTP round-trip is still the latest (including silent refreshes). */
  private finishReportRequest(seq: number): void {
    if (seq === this.reportLoadSeq) {
      this.loading = false;
      this.cdr.detectChanges();
    }
  }

  ngOnInit(): void {
    this.insightsTopStart = this.addDaysIso(this.insightsTopEnd, -30);
    this.loadDailyReport();
  }

  ngOnDestroy(): void {
    this.destroyInsightCharts();
  }

  setTab(tab: ReportTab): void {
    if (this.activeTab === 'insights' && tab !== 'insights') {
      this.destroyInsightCharts();
    }
    if (this.activeTab === tab) {
      return;
    }
    this.activeTab = tab;
    this.message = '';
    if (tab === 'daily') {
      this.loadDailyReport();
    } else if (tab === 'monthly') {
      this.loadMonthlyReport();
    } else if (tab === 'inout') {
      this.loadInOutReport();
    } else if (tab === 'hours') {
      this.loadHoursReport();
    } else if (tab === 'activity') {
      this.loadActivityReport();
    } else if (tab === 'insights') {
      if (!this.insightsLoaded) {
        this.loadInsights();
      } else {
        queueMicrotask(() => this.buildInsightCharts());
      }
    }
  }

  private addDaysIso(isoDate: string, deltaDays: number): string {
    const t = new Date(isoDate + 'T12:00:00');
    t.setDate(t.getDate() + deltaDays);
    return t.toISOString().slice(0, 10);
  }

  private destroyInsightCharts(): void {
    this.hourlyChart?.destroy();
    this.volumeChart?.destroy();
    this.topChart?.destroy();
    this.deptChart?.destroy();
    this.hourlyChart = undefined;
    this.volumeChart = undefined;
    this.topChart = undefined;
    this.deptChart = undefined;
  }

  loadInsights(): void {
    this.destroyInsightCharts();
    const gen = ++this.insightsReqSeq;
    const seq = this.startReportSequence(false);
    this.message = '';
    forkJoin({
      hourly: this.api.getAnalyticsHourly(this.insightsDate),
      volume: this.api.getAnalyticsDailyVolume(this.insightsVolumeEndDate, this.insightsVolumeDays),
      top: this.api.getAnalyticsTopScanners(this.insightsTopStart, this.insightsTopEnd, 10, true),
      dept: this.api.getAnalyticsByDepartment(this.insightsDate)
    }).subscribe({
      next: (data) => {
        if (gen !== this.insightsReqSeq) {
          return;
        }
        this.insightsHourly = data.hourly;
        this.insightsVolume = data.volume;
        this.insightsTop = data.top;
        this.insightsDept = data.dept;
        this.insightsLoaded = true;
        this.finishReportRequest(seq);
        queueMicrotask(() => {
          if (this.activeTab === 'insights' && gen === this.insightsReqSeq) {
            this.buildInsightCharts();
          }
        });
      },
      error: () => {
        if (gen !== this.insightsReqSeq) {
          return;
        }
        this.message = 'Failed to load behavior insights.';
        this.finishReportRequest(seq);
      }
    });
  }

  private chartFontFamily(): string {
    const raw = getComputedStyle(document.body).getPropertyValue('--font-sans').trim();
    return raw || "'Poppins', sans-serif";
  }

  private buildInsightCharts(): void {
    if (this.activeTab !== 'insights') {
      return;
    }
    this.destroyInsightCharts();
    Chart.defaults.font.family = this.chartFontFamily();
    Chart.defaults.color = '#475569';

    const navy = 'rgba(23, 39, 79, 0.85)';
    const navyLight = 'rgba(36, 58, 99, 0.55)';
    const accent = 'rgba(29, 78, 216, 0.75)';

    const hCan = this.hourlyCanvas?.nativeElement;
    if (hCan) {
      this.hourlyChart = new Chart(hCan, {
        type: 'bar',
        data: {
          labels: this.insightsHourly.map((x) => `${x.hour}`),
          datasets: [
            {
              label: 'Gate reads',
              data: this.insightsHourly.map((x) => x.count),
              backgroundColor: this.insightsHourly.map((_, i) => (i >= 9 && i <= 17 ? accent : navyLight)),
              borderRadius: 4
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { display: false }
          },
          scales: {
            x: {
              title: { display: true, text: 'Hour of day' },
              ticks: { maxRotation: 0 }
            },
            y: { beginAtZero: true, ticks: { precision: 0 } }
          }
        }
      });
    }

    const vCan = this.volumeCanvas?.nativeElement;
    if (vCan && this.insightsVolume.length) {
      this.volumeChart = new Chart(vCan, {
        type: 'line',
        data: {
          labels: this.insightsVolume.map((x) => this.shortDateLabel(x.date)),
          datasets: [
            {
              label: 'Total scans',
              data: this.insightsVolume.map((x) => x.totalScans),
              borderColor: navy,
              backgroundColor: 'rgba(23, 39, 79, 0.08)',
              fill: true,
              tension: 0.25
            },
            {
              label: 'Unique people',
              data: this.insightsVolume.map((x) => x.uniquePersons),
              borderColor: accent,
              backgroundColor: 'rgba(29, 78, 216, 0.06)',
              fill: true,
              tension: 0.25
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: 'bottom' } },
          scales: {
            y: { beginAtZero: true, ticks: { precision: 0 } }
          }
        }
      });
    }

    const tCan = this.topCanvas?.nativeElement;
    if (tCan && this.insightsTop.length) {
      this.topChart = new Chart(tCan, {
        type: 'bar',
        data: {
          labels: this.insightsTop.map((x) => x.fullName || x.personId),
          datasets: [
            {
              label: 'Reads (period)',
              data: this.insightsTop.map((x) => x.scanCount),
              backgroundColor: navy,
              borderRadius: 4
            }
          ]
        },
        options: {
          indexAxis: 'y',
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: {
            x: { beginAtZero: true, ticks: { precision: 0 } }
          }
        }
      });
    }

    const dCan = this.deptCanvas?.nativeElement;
    if (dCan && this.insightsDept.length) {
      const palette = ['#17274f', '#1d4ed8', '#0f766e', '#b45309', '#7c3aed', '#0e7490', '#be185d'];
      this.deptChart = new Chart(dCan, {
        type: 'doughnut',
        data: {
          labels: this.insightsDept.map((x) => x.department),
          datasets: [
            {
              data: this.insightsDept.map((x) => x.totalScans),
              backgroundColor: this.insightsDept.map((_, i) => palette[i % palette.length]),
              borderWidth: 1,
              borderColor: '#fff'
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { position: 'right' }
          }
        }
      });
    }
  }

  private shortDateLabel(iso: string): string {
    try {
      const d = new Date(iso + (iso.includes('T') ? '' : 'T12:00:00'));
      return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    } catch {
      return iso.slice(5, 10);
    }
  }

  onReportPageSizeChange(): void {
    this.dailyPage = 1;
    this.monthlyPage = 1;
    this.inOutPage = 1;
    this.hoursPage = 1;
    this.activityPage = 1;
    if (this.activeTab === 'daily') {
      this.loadDailyReport(false);
    } else if (this.activeTab === 'monthly') {
      this.loadMonthlyReport(false);
    } else if (this.activeTab === 'inout') {
      this.loadInOutReport(false);
    } else if (this.activeTab === 'hours') {
      this.loadHoursReport(false);
    } else if (this.activeTab === 'activity') {
      this.loadActivityReport(false);
    }
  }

  loadDailyReport(resetPage = true, silent = false): void {
    if (resetPage) {
      this.dailyPage = 1;
    }
    const gen = ++this.dailyReqSeq;
    const seq = this.startReportSequence(silent);
    // includePhotos=true + enrichFromDevice=true: DB first, then terminal photo-find for rows missing PhotoBase64 (common when DB was never back-filled).
    this.api.getDailyReport(this.today, this.dailyPage, this.reportPageSize, false, true, true).subscribe({
      next: (res) => {
        if (gen !== this.dailyReqSeq) {
          return;
        }
        const p = this.readPaged<DailyRow>(res);
        this.dailyReport = this.mapDailyItems(p.items as unknown[]);
        this.dailyPage = p.page;
        this.dailyTotalCount = p.totalCount;
        this.dailyTotalPages = p.totalPages;
        this.finishReportRequest(seq);
      },
      error: () => {
        if (gen !== this.dailyReqSeq) {
          return;
        }
        this.message = 'Failed to load daily report.';
        this.finishReportRequest(seq);
      }
    });
  }

  loadMonthlyReport(resetPage = true, silent = false): void {
    if (resetPage) {
      this.monthlyPage = 1;
    }
    const gen = ++this.monthlyReqSeq;
    const seq = this.startReportSequence(silent);
    this.api.getMonthlyReport(this.year, this.month, this.monthlyPage, this.reportPageSize, false).subscribe({
      next: (res) => {
        if (gen !== this.monthlyReqSeq) {
          return;
        }
        const p = this.readPaged<MonthlyRow>(res);
        this.monthlyReport = this.mapMonthlyItems(p.items as unknown[]);
        this.monthlyPage = p.page;
        this.monthlyTotalCount = p.totalCount;
        this.monthlyTotalPages = p.totalPages;
        this.finishReportRequest(seq);
      },
      error: () => {
        if (gen !== this.monthlyReqSeq) {
          return;
        }
        this.message = 'Failed to load monthly report.';
        this.finishReportRequest(seq);
      }
    });
  }

  loadInOutReport(resetPage = true, silent = false): void {
    if (resetPage) {
      this.inOutPage = 1;
    }
    const gen = ++this.inOutReqSeq;
    const seq = this.startReportSequence(silent);
    this.api.getInOutReport(this.inOutDate, this.inOutPage, this.reportPageSize, false).subscribe({
      next: (res) => {
        if (gen !== this.inOutReqSeq) {
          return;
        }
        const p = this.readPaged<InOutRow>(res);
        this.inOutReport = this.mapInOutItems(p.items as unknown[]);
        this.inOutPage = p.page;
        this.inOutTotalCount = p.totalCount;
        this.inOutTotalPages = p.totalPages;
        this.finishReportRequest(seq);
      },
      error: () => {
        if (gen !== this.inOutReqSeq) {
          return;
        }
        this.message = 'Failed to load IN/OUT report.';
        this.finishReportRequest(seq);
      }
    });
  }

  loadHoursReport(resetPage = true, silent = false): void {
    if (resetPage) {
      this.hoursPage = 1;
    }
    const gen = ++this.hoursReqSeq;
    const seq = this.startReportSequence(silent);
    this.api.getHoursTotalReport(this.hoursYear, this.hoursMonth, this.hoursPage, this.reportPageSize, false).subscribe({
      next: (res) => {
        if (gen !== this.hoursReqSeq) {
          return;
        }
        const p = this.readPaged<HoursRow>(res);
        this.hoursReport = this.mapHoursItems(p.items as unknown[]);
        this.hoursPage = p.page;
        this.hoursTotalCount = p.totalCount;
        this.hoursTotalPages = p.totalPages;
        this.finishReportRequest(seq);
      },
      error: () => {
        if (gen !== this.hoursReqSeq) {
          return;
        }
        this.message = 'Failed to load hours summary.';
        this.finishReportRequest(seq);
      }
    });
  }

  loadActivityReport(resetPage = true, silent = false): void {
    if (resetPage) {
      this.activityPage = 1;
    }
    const gen = ++this.activityReqSeq;
    const seq = this.startReportSequence(silent);
    if (!silent) {
      this.expandedPersonId = null;
    }
    this.api.getDailyActivityReport(this.activityDate, this.activityPage, this.reportPageSize, false).subscribe({
      next: (res) => {
        if (gen !== this.activityReqSeq) {
          return;
        }
        const p = this.readPaged<ActivityRow>(res);
        this.activityReport = this.mapActivityItems(p.items as unknown[]);
        this.activityPage = p.page;
        this.activityTotalCount = p.totalCount;
        this.activityTotalPages = p.totalPages;
        this.finishReportRequest(seq);
      },
      error: () => {
        if (gen !== this.activityReqSeq) {
          return;
        }
        this.message = 'Failed to load activity monitor.';
        this.finishReportRequest(seq);
      }
    });
  }

  goDailyPage(delta: number): void {
    const next = this.dailyPage + delta;
    if (next < 1 || next > this.dailyTotalPages) {
      return;
    }
    this.dailyPage = next;
    this.loadDailyReport(false);
  }

  goMonthlyPage(delta: number): void {
    const next = this.monthlyPage + delta;
    if (next < 1 || next > this.monthlyTotalPages) {
      return;
    }
    this.monthlyPage = next;
    this.loadMonthlyReport(false);
  }

  goInOutPage(delta: number): void {
    const next = this.inOutPage + delta;
    if (next < 1 || next > this.inOutTotalPages) {
      return;
    }
    this.inOutPage = next;
    this.loadInOutReport(false);
  }

  goHoursPage(delta: number): void {
    const next = this.hoursPage + delta;
    if (next < 1 || next > this.hoursTotalPages) {
      return;
    }
    this.hoursPage = next;
    this.loadHoursReport(false);
  }

  goActivityPage(delta: number): void {
    const next = this.activityPage + delta;
    if (next < 1 || next > this.activityTotalPages) {
      return;
    }
    this.activityPage = next;
    this.loadActivityReport(false);
  }

  deleteEnrollment(personId: string, event?: Event): void {
    event?.stopPropagation();
    const id = (personId ?? '').trim();
    if (!id) {
      return;
    }
    const ok = window.confirm(
      `Remove face enrollment for "${id}" from the access terminal and database?\n\nThis cannot be undone.`
    );
    if (!ok) {
      return;
    }
    this.faceDetailModal = null;
    this.deletingPersonId = id;
    this.message = '';
    this.api.deleteEmployeeEnrollment(id).subscribe({
      next: (res) => {
        this.deletingPersonId = null;
        this.cdr.detectChanges();
        this.message = typeof res?.message === 'string' ? res.message : 'Face enrollment removed.';
        this.removePersonFromCaches(id);
        queueMicrotask(() => this.refreshCurrentReport(true));
      },
      error: (err) => {
        this.deletingPersonId = null;
        this.cdr.detectChanges();
        const payload = err?.error;
        const detail =
          payload && typeof payload === 'object' && typeof (payload as { detail?: string }).detail === 'string'
            ? (payload as { detail: string }).detail.trim()
            : '';
        const apiMessage =
          (payload && typeof payload === 'object' && typeof (payload as { message?: string }).message === 'string'
            ? (payload as { message: string }).message
            : '') ||
          (typeof payload === 'string' ? payload : '') ||
          (err?.message ? err.message : '');
        this.message =
          detail && apiMessage ? `${apiMessage} — ${detail}` : detail || apiMessage || 'Delete failed.';
      }
    });
  }

  private refreshCurrentReport(silent = false): void {
    switch (this.activeTab) {
      case 'daily':
        this.loadDailyReport(false, silent);
        break;
      case 'monthly':
        this.loadMonthlyReport(false, silent);
        break;
      case 'inout':
        this.loadInOutReport(false, silent);
        break;
      case 'hours':
        this.loadHoursReport(false, silent);
        break;
      case 'activity':
        if (!silent) {
          this.expandedPersonId = null;
        }
        this.loadActivityReport(false, silent);
        break;
      default:
        break;
    }
  }

  private removePersonFromCaches(personId: string): void {
    const id = (personId ?? '').trim();
    if (!id) {
      return;
    }
    this.dailyReport = this.dailyReport.filter((r) => r.personId !== id);
    this.monthlyReport = this.monthlyReport.filter((r) => r.personId !== id);
    this.inOutReport = this.inOutReport.filter((r) => r.personId !== id);
    this.hoursReport = this.hoursReport.filter((r) => r.personId !== id);
    this.activityReport = this.activityReport.filter((r) => r.personId !== id);
    if (this.expandedPersonId === id) {
      this.expandedPersonId = null;
    }
  }

  /** Supports camelCase or PascalCase JSON from the API (fixes faces + Remove when casing differs). */
  private pickRP(r: Record<string, unknown>, camel: string, pascal: string): unknown {
    return r[camel] ?? r[pascal];
  }

  private mapDailyItems(items: unknown[]): DailyRow[] {
    return items.map((raw) => {
      const r = raw as Record<string, unknown>;
      const o = (c: string, p: string): string | null => {
        const v = this.pickRP(r, c, p);
        if (v == null) return null;
        return typeof v === 'string' ? v : String(v);
      };
      const s = (c: string, p: string) => {
        const v = this.pickRP(r, c, p);
        return v == null ? '' : String(v);
      };
      const first = this.pickRP(r, 'firstIn', 'FirstIn');
      const last = this.pickRP(r, 'lastOut', 'LastOut');
      return {
        personId: s('personId', 'PersonId').trim(),
        fullName: o('fullName', 'FullName'),
        photoBase64: o('photoBase64', 'PhotoBase64'),
        faceId: o('faceId', 'FaceId'),
        department: o('department', 'Department'),
        phone: o('phone', 'Phone'),
        idCardNumber: o('idCardNumber', 'IdCardNumber'),
        date: s('date', 'Date'),
        firstIn: first == null ? null : typeof first === 'string' ? first : String(first),
        lastOut: last == null ? null : typeof last === 'string' ? last : String(last),
        status: s('status', 'Status') || 'Present',
        totalHoursFormatted: o('totalHoursFormatted', 'TotalHoursFormatted') ?? undefined
      };
    });
  }

  private mapMonthlyItems(items: unknown[]): MonthlyRow[] {
    return items.map((raw) => {
      const r = raw as Record<string, unknown>;
      return {
        personId: String(this.pickRP(r, 'personId', 'PersonId') ?? '').trim(),
        year: Number(this.pickRP(r, 'year', 'Year') ?? 0),
        month: Number(this.pickRP(r, 'month', 'Month') ?? 0),
        presentDays: Number(this.pickRP(r, 'presentDays', 'PresentDays') ?? 0),
        absentDays: Number(this.pickRP(r, 'absentDays', 'AbsentDays') ?? 0)
      };
    });
  }

  private mapInOutItems(items: unknown[]): InOutRow[] {
    return items.map((raw) => {
      const r = raw as Record<string, unknown>;
      const name = this.pickRP(r, 'fullName', 'FullName');
      return {
        personId: String(this.pickRP(r, 'personId', 'PersonId') ?? '').trim(),
        fullName: name == null ? null : typeof name === 'string' ? name : String(name),
        transactionTime: String(this.pickRP(r, 'transactionTime', 'TransactionTime') ?? ''),
        deviceSn: String(this.pickRP(r, 'deviceSn', 'DeviceSn') ?? ''),
        eventLabel: String(this.pickRP(r, 'eventLabel', 'EventLabel') ?? '')
      };
    });
  }

  private mapHoursItems(items: unknown[]): HoursRow[] {
    return items.map((raw) => {
      const r = raw as Record<string, unknown>;
      return {
        personId: String(this.pickRP(r, 'personId', 'PersonId') ?? '').trim(),
        year: Number(this.pickRP(r, 'year', 'Year') ?? 0),
        month: Number(this.pickRP(r, 'month', 'Month') ?? 0),
        daysPresent: Number(this.pickRP(r, 'daysPresent', 'DaysPresent') ?? 0),
        totalHoursFormatted: String(this.pickRP(r, 'totalHoursFormatted', 'TotalHoursFormatted') ?? '')
      };
    });
  }

  private mapActivityItems(items: unknown[]): ActivityRow[] {
    return items.map((raw) => {
      const r = raw as Record<string, unknown>;
      const eventsRaw = this.pickRP(r, 'events', 'Events');
      const arr = Array.isArray(eventsRaw) ? eventsRaw : [];
      const events: ActivityEvent[] = arr.map((e) => {
        const ev = e as Record<string, unknown>;
        const gap = this.pickRP(ev, 'minutesSincePrevious', 'MinutesSincePrevious');
        return {
          sequence: Number(this.pickRP(ev, 'sequence', 'Sequence') ?? 0),
          totalForPerson: Number(this.pickRP(ev, 'totalForPerson', 'TotalForPerson') ?? 0),
          time: String(this.pickRP(ev, 'time', 'Time') ?? ''),
          deviceSn: String(this.pickRP(ev, 'deviceSn', 'DeviceSn') ?? ''),
          accessRoleLabel: String(this.pickRP(ev, 'accessRoleLabel', 'AccessRoleLabel') ?? ''),
          minutesSincePrevious: gap == null ? null : Number(gap)
        };
      });
      const name = this.pickRP(r, 'fullName', 'FullName');
      const note = this.pickRP(r, 'monitoringNote', 'MonitoringNote');
      const review = this.pickRP(r, 'requiresHrReview', 'RequiresHrReview');
      return {
        personId: String(this.pickRP(r, 'personId', 'PersonId') ?? '').trim(),
        fullName: name == null ? null : typeof name === 'string' ? name : String(name),
        date: String(this.pickRP(r, 'date', 'Date') ?? ''),
        accessCount: Number(this.pickRP(r, 'accessCount', 'AccessCount') ?? 0),
        firstAccessTime: String(this.pickRP(r, 'firstAccessTime', 'FirstAccessTime') ?? ''),
        lastAccessTime: String(this.pickRP(r, 'lastAccessTime', 'LastAccessTime') ?? ''),
        timeOnPremisesFormatted: String(this.pickRP(r, 'timeOnPremisesFormatted', 'TimeOnPremisesFormatted') ?? ''),
        requiresHrReview: Boolean(review),
        monitoringNote: note == null ? '' : typeof note === 'string' ? note : String(note),
        events
      };
    });
  }

  private readPaged<T>(res: unknown): {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
  } {
    const o = res as PagedReport<T> & Record<string, unknown>;
    const rawItems = o.items ?? o['Items'];
    const items = (Array.isArray(rawItems) ? rawItems : []) as T[];
    const totalCount = Number(o.totalCount ?? o['TotalCount'] ?? 0);
    const page = Number(o.page ?? o['Page'] ?? 1);
    const pageSize = Number(o.pageSize ?? o['PageSize'] ?? this.reportPageSize);
    let totalPages = Number(o.totalPages ?? o['TotalPages'] ?? NaN);
    if (!Number.isFinite(totalPages) || totalPages < 1) {
      totalPages = Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)));
    }
    return { items, totalCount, page, pageSize, totalPages };
  }

  toggleExpand(personId: string): void {
    this.expandedPersonId = this.expandedPersonId === personId ? null : personId;
  }

  filterActivity(): ActivityRow[] {
    let rows = this.filterRows(this.activityReport, ['personId', 'fullName', 'monitoringNote']);
    if (this.showMultiAccessOnly) {
      rows = rows.filter((r) => r.accessCount >= 3);
    }
    return rows;
  }

  activityStats(): { total: number; review: number; multi: number } {
    const pageRows = this.activityReport;
    return {
      total: this.activityTotalCount,
      review: pageRows.filter((r) => r.requiresHrReview).length,
      multi: pageRows.filter((r) => r.accessCount >= 3).length
    };
  }

  filterDaily(): DailyRow[] {
    return this.filterRows(this.dailyReport, [
      'personId',
      'fullName',
      'faceId',
      'department',
      'phone',
      'idCardNumber',
      'status',
      'totalHoursFormatted'
    ]);
  }

  /** Safe data URL for enrollment photo (DB stores plain base64; terminal may return JPEG or PNG). */
  dailyFaceDataUrl(row: DailyRow): string {
    const b = (row.photoBase64 ?? '').trim();
    if (!b) {
      return '';
    }
    if (b.startsWith('data:')) {
      return b;
    }
    if (b.startsWith('iVBOR')) {
      return `data:image/png;base64,${b}`;
    }
    return `data:image/jpeg;base64,${b}`;
  }

  openFaceDetail(row: DailyRow, event?: Event): void {
    event?.stopPropagation();
    this.faceDetailModal = row;
  }

  closeFaceDetail(): void {
    this.faceDetailModal = null;
  }

  @HostListener('document:keydown.escape')
  onFaceModalEscape(): void {
    if (this.faceDetailModal) {
      this.faceDetailModal = null;
    }
  }

  /** Page-level counts after search filter (current table page). */
  dailyStatusCounts(): { present: number; absent: number; other: number } {
    let present = 0;
    let absent = 0;
    let other = 0;
    for (const r of this.filterDaily()) {
      const b = this.statusBucket(r.status);
      if (b === 'present') present++;
      else if (b === 'absent') absent++;
      else other++;
    }
    return { present, absent, other };
  }

  isDailyPresent(status: string): boolean {
    return this.statusBucket(status) === 'present';
  }

  isDailyAbsent(status: string): boolean {
    return this.statusBucket(status) === 'absent';
  }

  private statusBucket(status: string): 'present' | 'absent' | 'other' {
    const s = (status ?? '').trim().toLowerCase();
    if (s === 'present' || s.startsWith('present ')) return 'present';
    if (s === 'absent' || s.startsWith('absent ')) return 'absent';
    return 'other';
  }

  filterMonthly(): MonthlyRow[] {
    return this.filterRows(this.monthlyReport, ['personId']);
  }

  filterInOut(): InOutRow[] {
    return this.filterRows(this.inOutReport, ['personId', 'fullName', 'deviceSn', 'eventLabel']);
  }

  filterHours(): HoursRow[] {
    return this.filterRows(this.hoursReport, ['personId', 'totalHoursFormatted']);
  }

  private filterRows<T>(rows: T[], keys: (keyof T)[]): T[] {
    const q = this.searchText.trim().toLowerCase();
    if (!q) {
      return rows;
    }
    return rows.filter((r) =>
      keys.some((k) => String((r as Record<string, unknown>)[k as string] ?? '').toLowerCase().includes(q))
    );
  }

  exportDailyExcel(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getDailyReport(this.today, 1, this.reportPageSize, true, false).subscribe({
      next: (res) => {
        const items = this.mapDailyItems(this.readPaged<DailyRow>(res).items as unknown[]);
        const rows = items.map((r) => ({
          'Person ID': r.personId,
          'Person name': r.fullName ?? '',
          'Face ID': r.faceId ?? '',
          Date: r.date,
          'First In': r.firstIn ?? '',
          'Last Out': r.lastOut ?? '',
          Status: r.status,
          'Total Hrs': r.totalHoursFormatted ?? ''
        }));
        exportToExcel(`daily-report-${this.today}.xlsx`, 'Daily', rows);
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportDailyPdf(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getDailyReport(this.today, 1, this.reportPageSize, true, false).subscribe({
      next: (res) => {
        const data = this.mapDailyItems(this.readPaged<DailyRow>(res).items as unknown[]);
        exportToPdf(
          `Daily attendance — ${this.today}`,
          `daily-report-${this.today}.pdf`,
          [
            ['Person ID', 'Person name', 'Face ID', 'Date', 'First In', 'Last Out', 'Status', 'Total Hrs']
          ],
          data.map((r) => [
            r.personId,
            r.fullName ?? '—',
            r.faceId ?? '—',
            r.date,
            String(r.firstIn ?? '—'),
            String(r.lastOut ?? '—'),
            r.status,
            r.totalHoursFormatted ?? '—'
          ])
        );
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportMonthlyExcel(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getMonthlyReport(this.year, this.month, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const items = this.readPaged<MonthlyRow>(res).items;
        const rows = items.map((r) => ({
          'Person ID': r.personId,
          Year: r.year,
          Month: r.month,
          'Present Days': r.presentDays,
          'Absent Days': r.absentDays
        }));
        exportToExcel(`monthly-report-${this.year}-${this.month}.xlsx`, 'Monthly', rows);
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportMonthlyPdf(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getMonthlyReport(this.year, this.month, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const data = this.readPaged<MonthlyRow>(res).items;
        exportToPdf(
          `Monthly attendance — ${this.year}-${String(this.month).padStart(2, '0')}`,
          `monthly-report-${this.year}-${this.month}.pdf`,
          [['Person ID', 'Year', 'Month', 'Present', 'Absent']],
          data.map((r) => [r.personId, r.year, r.month, r.presentDays, r.absentDays])
        );
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportInOutExcel(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getInOutReport(this.inOutDate, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const items = this.readPaged<InOutRow>(res).items;
        const rows = items.map((r) => ({
          'Person ID': r.personId,
          Name: r.fullName ?? '',
          Time: r.transactionTime,
          Device: r.deviceSn,
          Event: r.eventLabel
        }));
        exportToExcel(`inout-report-${this.inOutDate}.xlsx`, 'IN_OUT', rows);
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportInOutPdf(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getInOutReport(this.inOutDate, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const data = this.readPaged<InOutRow>(res).items;
        exportToPdf(
          `IN/OUT activity — ${this.inOutDate}`,
          `inout-report-${this.inOutDate}.pdf`,
          [['Person ID', 'Name', 'Time', 'Device', 'Event']],
          data.map((r) => [
            r.personId,
            r.fullName ?? '—',
            this.formatDt(r.transactionTime),
            r.deviceSn || '—',
            r.eventLabel
          ])
        );
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportHoursExcel(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getHoursTotalReport(this.hoursYear, this.hoursMonth, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const items = this.readPaged<HoursRow>(res).items;
        const rows = items.map((r) => ({
          'Person ID': r.personId,
          Year: r.year,
          Month: r.month,
          'Days Present': r.daysPresent,
          'Total Hrs (month)': r.totalHoursFormatted
        }));
        exportToExcel(`hours-total-${this.hoursYear}-${this.hoursMonth}.xlsx`, 'Hours', rows);
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportHoursPdf(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getHoursTotalReport(this.hoursYear, this.hoursMonth, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const data = this.readPaged<HoursRow>(res).items;
        exportToPdf(
          `Total hours — ${this.hoursYear}-${String(this.hoursMonth).padStart(2, '0')}`,
          `hours-total-${this.hoursYear}-${this.hoursMonth}.pdf`,
          [['Person ID', 'Year', 'Month', 'Days Present', 'Total Hrs']],
          data.map((r) => [r.personId, r.year, r.month, r.daysPresent, r.totalHoursFormatted])
        );
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportActivityExcel(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getDailyActivityReport(this.activityDate, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const items = this.readPaged<ActivityRow>(res).items;
        const flat: Record<string, unknown>[] = [];
        for (const r of items) {
          if (this.showMultiAccessOnly && r.accessCount < 3) {
            continue;
          }
          const q = this.searchText.trim().toLowerCase();
          if (q) {
            const hay = `${r.personId} ${r.fullName ?? ''} ${r.monitoringNote}`.toLowerCase();
            if (!hay.includes(q)) {
              continue;
            }
          }
          for (const ev of r.events ?? []) {
            flat.push({
              Date: r.date,
              'Person ID': r.personId,
              Name: r.fullName ?? '',
              'Access #': ev.sequence,
              'Of total': ev.totalForPerson,
              Time: ev.time,
              Device: ev.deviceSn,
              Role: ev.accessRoleLabel,
              'Min since prev': ev.minutesSincePrevious ?? '',
              'HR note': r.monitoringNote,
              Review: r.requiresHrReview ? 'Yes' : 'No'
            });
          }
        }
        exportToExcel(`activity-monitor-${this.activityDate}.xlsx`, 'Activity', flat);
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  exportActivityPdf(): void {
    this.exportBusy = true;
    this.message = '';
    this.api.getDailyActivityReport(this.activityDate, 1, this.reportPageSize, true).subscribe({
      next: (res) => {
        const items = this.readPaged<ActivityRow>(res).items;
        const rows = items.filter((r) => {
          if (this.showMultiAccessOnly && r.accessCount < 3) {
            return false;
          }
          const q = this.searchText.trim().toLowerCase();
          if (!q) {
            return true;
          }
          const hay = `${r.personId} ${r.fullName ?? ''} ${r.monitoringNote}`.toLowerCase();
          return hay.includes(q);
        });
        const body: (string | number)[][] = [];
        for (const r of rows) {
          body.push([
            r.personId,
            r.fullName ?? '—',
            r.accessCount,
            r.timeOnPremisesFormatted,
            r.requiresHrReview ? 'Review' : '—'
          ]);
          for (const ev of r.events ?? []) {
            body.push([
              '',
              `  ${ev.sequence}/${ev.totalForPerson}`,
              this.formatDt(ev.time),
              ev.accessRoleLabel,
              ev.minutesSincePrevious ?? '—'
            ]);
          }
        }
        exportToPdf(
          `Activity monitor — ${this.activityDate}`,
          `activity-monitor-${this.activityDate}.pdf`,
          [['Person ID', 'Name', 'Reads', 'On premises', 'Flag']],
          body
        );
        this.exportBusy = false;
      },
      error: () => {
        this.message = 'Export failed.';
        this.exportBusy = false;
      }
    });
  }

  formatDt(value: string): string {
    try {
      return new Date(value).toLocaleString();
    } catch {
      return value;
    }
  }

  formatPeriod(year: number, month: number): string {
    return `${year}-${month < 10 ? '0' : ''}${month}`;
  }
}

interface DailyRow {
  personId: string;
  fullName?: string | null;
  photoBase64?: string | null;
  faceId?: string | null;
  department?: string | null;
  phone?: string | null;
  idCardNumber?: string | null;
  date: string;
  firstIn: string | null;
  lastOut: string | null;
  status: string;
  totalHoursFormatted?: string;
}

interface MonthlyRow {
  personId: string;
  year: number;
  month: number;
  presentDays: number;
  absentDays: number;
}

interface InOutRow {
  personId: string;
  fullName: string | null;
  transactionTime: string;
  deviceSn: string;
  eventLabel: string;
}

interface HoursRow {
  personId: string;
  year: number;
  month: number;
  daysPresent: number;
  totalHoursFormatted: string;
}

interface ActivityEvent {
  sequence: number;
  totalForPerson: number;
  time: string;
  deviceSn: string;
  accessRoleLabel: string;
  minutesSincePrevious: number | null;
}

interface ActivityRow {
  personId: string;
  fullName: string | null;
  date: string;
  accessCount: number;
  firstAccessTime: string;
  lastAccessTime: string;
  timeOnPremisesFormatted: string;
  requiresHrReview: boolean;
  monitoringNote: string;
  events: ActivityEvent[];
}
