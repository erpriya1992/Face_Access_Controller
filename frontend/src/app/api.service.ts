import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { AppConfigService } from './app-config.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  // Loaded from `/assets/app-config.json` at app startup.
  private baseUrl: string;
  private hubUrl: string;
  private token = localStorage.getItem('fac_token') ?? '';
  private hubConnection?: signalR.HubConnection;

  constructor(private http: HttpClient, private cfg: AppConfigService) {
    this.baseUrl = this.cfg.apiBaseUrl;
    this.hubUrl = this.cfg.hubUrl;
  }

  setToken(token: string): void {
    this.token = token;
    localStorage.setItem('fac_token', token);
  }

  clearToken(): void {
    this.token = '';
    localStorage.removeItem('fac_token');
  }

  isAuthenticated(): boolean {
    return !!this.token;
  }

  login(username: string, password: string) {
    return this.http.post<{ token: string; username: string; role: string }>(
      `${this.baseUrl}/auth/login`,
      { username, password }
    );
  }

  getAdminConfig() {
    return this.http.get<{ username: string; password?: string }>(`${this.baseUrl}/config/admin`);
  }

  getDeviceStatus() {
    return this.http.get<DeviceConnectivityStatus>(`${this.baseUrl}/config/device-status`, { headers: this.authHeaders() });
  }

  /** Registered face readers (for enrollment target and admin). */
  getFaceDevices() {
    return this.http.get<FaceDeviceListDto[]>(`${this.baseUrl}/face-devices`, { headers: this.authHeaders() });
  }

  getFaceDeviceById(id: number) {
    return this.http.get<FaceDeviceDetailDto>(`${this.baseUrl}/face-devices/${id}`, { headers: this.authHeaders() });
  }

  probeFaceDevice(deviceIp: string) {
    return this.http.post<FaceDeviceProbeDto>(`${this.baseUrl}/face-devices/probe`, { deviceIp }, { headers: this.authHeaders() });
  }

  saveFaceDevice(payload: SaveFaceDeviceRequest, id?: number | null) {
    if (id != null) {
      return this.http.put<FaceDeviceSaveResult>(`${this.baseUrl}/face-devices/${id}`, payload, { headers: this.authHeaders() });
    }
    return this.http.post<FaceDeviceSaveResult>(`${this.baseUrl}/face-devices`, payload, { headers: this.authHeaders() });
  }

  registerFace(payload: any) {
    return this.http.post<{ message?: string; faceDeviceUiApplied?: boolean | null; photoBase64?: string | null }>(
      `${this.baseUrl}/registration/face`,
      payload,
      {
        headers: this.authHeaders()
      }
    );
  }

  /** Removes person from face terminal (via middleware) and from the local employee table. */
  deleteEmployeeEnrollment(personId: string) {
    return this.http.delete<{ message?: string }>(`${this.baseUrl}/employees/${encodeURIComponent(personId)}`, {
      headers: this.authHeaders()
    });
  }

  /** All enrolled users for Settings (door access control). */
  getEmployeesForSettings() {
    return this.http.get<EmployeeSettingsRow[]>(`${this.baseUrl}/employees`, { headers: this.authHeaders() });
  }

  updateEmployeeDoorAccess(personId: string, doorAccessAllowed: boolean) {
    return this.http.patch<{
      personId: string;
      doorAccessAllowed: boolean;
      syncedToDevice: boolean;
      warning?: string | null;
    }>(`${this.baseUrl}/employees/${encodeURIComponent(personId)}/door-access`, { doorAccessAllowed }, { headers: this.authHeaders() });
  }

  syncTransactions() {
    return this.http.post<{ synced: number }>(`${this.baseUrl}/transactions/sync`, {}, { headers: this.authHeaders() });
  }

  getLiveTransactions(startDateTime?: string, endDateTime?: string) {
    const query = new URLSearchParams();
    if (startDateTime) query.set('startDateTime', startDateTime);
    if (endDateTime) query.set('endDateTime', endDateTime);
    const suffix = query.toString() ? `?${query.toString()}` : '';

    return this.http.get<any[]>(`${this.baseUrl}/transactions/live${suffix}`, { headers: this.authHeaders() });
  }

  /**
   * Daily report. Default is fast: no embedded photos (use postDailyReportPhotoBatch after load).
   * Set includePhotos true for a single response with DB photos; enrichFromDevice hits the terminal (slow).
   */
  getDailyReport(
    date: string,
    page = 1,
    pageSize = 50,
    all = false,
    includePhotos = false,
    enrichFromDevice = false
  ) {
    const q = this.reportPagingQuery(page, pageSize, all);
    q.set('date', date);
    if (includePhotos) {
      q.set('includePhotos', 'true');
    }
    if (enrichFromDevice) {
      q.set('enrichMissingPhotosFromDevice', 'true');
    }
    return this.http.get<PagedReport<unknown>>(`${this.baseUrl}/reports/daily?${q}`, { headers: this.authHeaders() });
  }

  /** Enrollment thumbnails only — one DB query; call after fast daily load. */
  postDailyReportPhotoBatch(personIds: string[]) {
    return this.http.post<{ items: DailyPhotoBatchRow[] }>(
      `${this.baseUrl}/reports/daily/photo-batch`,
      { personIds },
      { headers: this.authHeaders() }
    );
  }

  getMonthlyReport(year: number, month: number, page = 1, pageSize = 50, all = false) {
    const q = this.reportPagingQuery(page, pageSize, all);
    q.set('year', String(year));
    q.set('month', String(month));
    return this.http.get<PagedReport<unknown>>(`${this.baseUrl}/reports/monthly?${q}`, { headers: this.authHeaders() });
  }

  getInOutReport(date: string, page = 1, pageSize = 50, all = false) {
    const q = this.reportPagingQuery(page, pageSize, all);
    q.set('date', date);
    return this.http.get<PagedReport<unknown>>(`${this.baseUrl}/reports/inout?${q}`, { headers: this.authHeaders() });
  }

  getHoursTotalReport(year: number, month: number, page = 1, pageSize = 50, all = false) {
    const q = this.reportPagingQuery(page, pageSize, all);
    q.set('year', String(year));
    q.set('month', String(month));
    return this.http.get<PagedReport<unknown>>(`${this.baseUrl}/reports/hours-total?${q}`, { headers: this.authHeaders() });
  }

  getDailyActivityReport(date: string, page = 1, pageSize = 50, all = false) {
    const q = this.reportPagingQuery(page, pageSize, all);
    q.set('date', date);
    return this.http.get<PagedReport<unknown>>(`${this.baseUrl}/reports/daily-activity?${q}`, { headers: this.authHeaders() });
  }

  private reportPagingQuery(page: number, pageSize: number, all: boolean): URLSearchParams {
    const q = new URLSearchParams();
    if (all) {
      q.set('all', 'true');
    } else {
      q.set('page', String(page));
      q.set('pageSize', String(pageSize));
    }
    return q;
  }

  getAnalyticsHourly(date: string) {
    return this.http.get<HourlyAccessItem[]>(`${this.baseUrl}/analytics/hourly?date=${date}`, { headers: this.authHeaders() });
  }

  getAnalyticsDailyVolume(endDate: string, days: number) {
    return this.http.get<DailyVolumeItem[]>(
      `${this.baseUrl}/analytics/daily-volume?endDate=${endDate}&days=${days}`,
      { headers: this.authHeaders() }
    );
  }

  getAnalyticsTopScanners(start: string, end: string, take = 10, excludeVisitors = true) {
    const ex = excludeVisitors ? 'true' : 'false';
    return this.http.get<TopScannerItem[]>(
      `${this.baseUrl}/analytics/top-scanners?start=${start}&end=${end}&take=${take}&excludeVisitors=${ex}`,
      { headers: this.authHeaders() }
    );
  }

  getAnalyticsByDepartment(date: string) {
    return this.http.get<DepartmentAccessItem[]>(`${this.baseUrl}/analytics/by-department?date=${date}`, {
      headers: this.authHeaders()
    });
  }

  async connectLive(onMessage: () => void): Promise<void> {
    if (this.hubConnection) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => this.token
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('transactions-updated', () => onMessage());
    await this.hubConnection.start();
  }

  private authHeaders(): HttpHeaders {
    return new HttpHeaders({
      Authorization: `Bearer ${this.token}`
    });
  }
}

export interface DeviceConnectivityStatus {
  middlewareOnline: boolean;
  deviceOnline: boolean;
  middlewareUrl: string;
  faceDeviceTarget: string;
  middlewareDetail: string | null;
  deviceDetail: string | null;
  checkedAtUtc: string;
}

export interface PagedReport<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface DailyPhotoBatchRow {
  personId: string;
  photoBase64: string | null;
}

export interface EmployeeSettingsRow {
  id: number;
  personId: string;
  fullName: string;
  department?: string | null;
  phone?: string | null;
  idCardNumber?: string | null;
  doorAccessAllowed: boolean;
  createdAt: string;
  faceDeviceId?: number | null;
  faceDeviceName?: string | null;
}

export interface FaceDeviceListDto {
  id: number;
  name: string;
  siteControl?: string | null;
  deviceIp: string;
  direction: string;
  isActive: boolean;
  sortOrder: number;
}

export interface FaceDeviceSettings {
  siteControl?: string | null;
  unitNo: number;
  destIp?: string | null;
  direction: string;
  defaultEnrollerFingerprintOrFace: boolean;
  timeZone: string;
  releaseTimeMs: number;
  recognitionScore: number;
  strangerDetection: boolean;
  strangerThreshold: number;
  voiceMode: string;
  displayMode: string;
  livenessEnabled: boolean;
  enableTransaction: boolean;
  multiFaceDetection: string;
  strangerVoiceMode: string;
  displayCustomization: string;
  wiegandOutput: string;
  enableFaceRecognitionInterval: boolean;
  faceRecognitionIntervalMs: number;
  qrVerificationMode: string;
  photoDisplay: string;
  pushConfigToDevice: boolean;
}

export interface SaveFaceDeviceRequest {
  name: string;
  description?: string | null;
  deviceIp: string;
  devicePassword?: string | null;
  sortOrder: number;
  isActive: boolean;
  settings: FaceDeviceSettings;
}

export interface FaceDeviceDetailDto {
  id: number;
  name: string;
  description?: string | null;
  deviceIp: string;
  devicePassword?: string | null;
  sortOrder: number;
  isActive: boolean;
  settings: FaceDeviceSettings;
}

export interface FaceDeviceSaveResult {
  device: FaceDeviceDetailDto;
  configPushedToDevice: boolean;
  configPushWarning?: string | null;
}

export interface FaceDeviceProbeDto {
  reachable: boolean;
  deviceKey?: string | null;
  detail?: string | null;
}

export interface HourlyAccessItem {
  hour: number;
  count: number;
}

export interface DailyVolumeItem {
  date: string;
  totalScans: number;
  uniquePersons: number;
}

export interface TopScannerItem {
  personId: string;
  fullName: string | null;
  scanCount: number;
}

export interface DepartmentAccessItem {
  department: string;
  totalScans: number;
  uniqueEmployees: number;
}
