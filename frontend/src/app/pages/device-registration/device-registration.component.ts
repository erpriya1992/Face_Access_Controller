import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApiService,
  FaceDeviceDetailDto,
  FaceDeviceListDto,
  FaceDeviceProbeDto,
  FaceDeviceSettings,
  SaveFaceDeviceRequest
} from '../../api.service';

type DeviceForm = SaveFaceDeviceRequest;

@Component({
  selector: 'app-device-registration',
  imports: [CommonModule, FormsModule],
  templateUrl: './device-registration.component.html',
  styleUrl: './device-registration.component.scss'
})
export class DeviceRegistrationComponent implements OnInit {
  readonly siteControlOptions = ['OFFA', 'HQ', 'Branch A', 'Branch B'];
  readonly directionOptions = ['Entry', 'Exit'];
  readonly timeZoneOptions = ['GMT+8', 'GMT+5:30', 'GMT+0', 'GMT-5'];
  readonly strangerThresholdOptions = [1, 2, 3, 4, 5];
  readonly voiceModeOptions = [
    { value: 'NoVoice', label: 'No Voice Announcement' },
    { value: 'Name', label: 'Announce Name' }
  ];
  readonly displayModeOptions = [
    { value: 'DisplayName', label: 'Display Name' },
    { value: 'DisplayId', label: 'Display ID' },
    { value: 'DisplayNone', label: 'Display None' }
  ];
  readonly multiFaceOptions = [
    { value: 'Multiple', label: 'Recognize Multiple Faces' },
    { value: 'Single', label: 'Recognize Single Face' }
  ];
  readonly strangerVoiceOptions = [
    { value: 'StrangerAlarm', label: 'Stranger Alarm' },
    { value: 'NoVoice', label: 'No Voice' }
  ];
  readonly wiegandOptions = [
    { value: 'WG26', label: 'WG26 Card No' },
    { value: 'WG34', label: 'WG34 Card No' }
  ];
  readonly qrModeOptions = [
    { value: 'ThirdParty', label: 'Third Party Platform' },
    { value: 'Local', label: 'Local Verification' }
  ];
  readonly photoDisplayOptions = [
    { value: 'OnSite', label: 'On-Site Photo' },
    { value: 'Registered', label: 'Registered Photo' }
  ];

  devices: FaceDeviceListDto[] = [];
  loading = false;
  saving = false;
  probing = false;
  message = '';
  messageTone: 'ok' | 'err' | 'info' = 'info';
  probeResult: FaceDeviceProbeDto | null = null;
  editingId: number | null = null;
  activeTab: 'details' | 'network' = 'details';

  form: DeviceForm = this.emptyForm();

  constructor(
    private api: ApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadDevices();
  }

  private defaultSettings(): FaceDeviceSettings {
    return {
      siteControl: 'OFFA',
      unitNo: 1,
      destIp: '',
      direction: 'Entry',
      defaultEnrollerFingerprintOrFace: false,
      timeZone: 'GMT+8',
      releaseTimeMs: 500,
      recognitionScore: 80,
      strangerDetection: true,
      strangerThreshold: 3,
      voiceMode: 'NoVoice',
      displayMode: 'DisplayName',
      livenessEnabled: true,
      enableTransaction: true,
      multiFaceDetection: 'Multiple',
      strangerVoiceMode: 'StrangerAlarm',
      displayCustomization: '{name}',
      wiegandOutput: 'WG26',
      enableFaceRecognitionInterval: false,
      faceRecognitionIntervalMs: 2000,
      qrVerificationMode: 'ThirdParty',
      photoDisplay: 'OnSite',
      pushConfigToDevice: true
    };
  }

  private emptyForm(): DeviceForm {
    return {
      name: '',
      description: '',
      deviceIp: '',
      devicePassword: '',
      sortOrder: 0,
      isActive: true,
      settings: this.defaultSettings()
    };
  }

  loadDevices(): void {
    this.loading = true;
    this.message = '';
    this.api.getFaceDevices().subscribe({
      next: (rows) => {
        this.devices = (rows ?? []).map((r) => this.normalizeListRow(r));
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.setMessage('Could not load registered devices.', 'err');
        this.cdr.markForCheck();
      }
    });
  }

  private normalizeListRow(raw: FaceDeviceListDto | Record<string, unknown>): FaceDeviceListDto {
    const r = raw as Record<string, unknown>;
    const pick = (c: string, p: string) => r[c] ?? r[p];
    return {
      id: Number(pick('id', 'Id') ?? 0),
      name: String(pick('name', 'Name') ?? ''),
      siteControl: (pick('siteControl', 'SiteControl') as string | null | undefined) ?? null,
      deviceIp: String(pick('deviceIp', 'DeviceIp') ?? ''),
      direction: String(pick('direction', 'Direction') ?? 'Entry'),
      isActive: Boolean(pick('isActive', 'IsActive') ?? true),
      sortOrder: Number(pick('sortOrder', 'SortOrder') ?? 0)
    };
  }

  startEdit(device: FaceDeviceListDto): void {
    this.editingId = device.id;
    this.probeResult = null;
    this.message = '';
    this.activeTab = 'details';
    this.api.getFaceDeviceById(device.id).subscribe({
      next: (detail) => {
        this.form = this.detailToForm(this.normalizeDetail(detail));
        this.cdr.markForCheck();
      },
      error: () => {
        this.setMessage('Could not load device details.', 'err');
        this.cdr.markForCheck();
      }
    });
  }

  private normalizeDetail(raw: FaceDeviceDetailDto | Record<string, unknown>): FaceDeviceDetailDto {
    const r = raw as Record<string, unknown>;
    const pick = (c: string, p: string) => r[c] ?? r[p];
    const settingsRaw = (pick('settings', 'Settings') ?? {}) as Record<string, unknown>;
    const sp = (c: string, p: string) => settingsRaw[c] ?? settingsRaw[p];
    const defaults = this.defaultSettings();
    return {
      id: Number(pick('id', 'Id') ?? 0),
      name: String(pick('name', 'Name') ?? ''),
      description: (pick('description', 'Description') as string | null | undefined) ?? '',
      deviceIp: String(pick('deviceIp', 'DeviceIp') ?? ''),
      devicePassword: (pick('devicePassword', 'DevicePassword') as string | null | undefined) ?? '',
      sortOrder: Number(pick('sortOrder', 'SortOrder') ?? 0),
      isActive: Boolean(pick('isActive', 'IsActive') ?? true),
      settings: {
        ...defaults,
        siteControl: (sp('siteControl', 'SiteControl') as string | null | undefined) ?? defaults.siteControl,
        unitNo: Number(sp('unitNo', 'UnitNo') ?? defaults.unitNo),
        destIp: (sp('destIp', 'DestIp') as string | null | undefined) ?? '',
        direction: String(sp('direction', 'Direction') ?? defaults.direction),
        defaultEnrollerFingerprintOrFace: Boolean(
          sp('defaultEnrollerFingerprintOrFace', 'DefaultEnrollerFingerprintOrFace') ?? false
        ),
        timeZone: String(sp('timeZone', 'TimeZone') ?? defaults.timeZone),
        releaseTimeMs: Number(sp('releaseTimeMs', 'ReleaseTimeMs') ?? defaults.releaseTimeMs),
        recognitionScore: Number(sp('recognitionScore', 'RecognitionScore') ?? defaults.recognitionScore),
        strangerDetection: Boolean(sp('strangerDetection', 'StrangerDetection') ?? defaults.strangerDetection),
        strangerThreshold: Number(sp('strangerThreshold', 'StrangerThreshold') ?? defaults.strangerThreshold),
        voiceMode: String(sp('voiceMode', 'VoiceMode') ?? defaults.voiceMode),
        displayMode: String(sp('displayMode', 'DisplayMode') ?? defaults.displayMode),
        livenessEnabled: Boolean(sp('livenessEnabled', 'LivenessEnabled') ?? defaults.livenessEnabled),
        enableTransaction: Boolean(sp('enableTransaction', 'EnableTransaction') ?? defaults.enableTransaction),
        multiFaceDetection: String(sp('multiFaceDetection', 'MultiFaceDetection') ?? defaults.multiFaceDetection),
        strangerVoiceMode: String(sp('strangerVoiceMode', 'StrangerVoiceMode') ?? defaults.strangerVoiceMode),
        displayCustomization: String(sp('displayCustomization', 'DisplayCustomization') ?? defaults.displayCustomization),
        wiegandOutput: String(sp('wiegandOutput', 'WiegandOutput') ?? defaults.wiegandOutput),
        enableFaceRecognitionInterval: Boolean(
          sp('enableFaceRecognitionInterval', 'EnableFaceRecognitionInterval') ?? false
        ),
        faceRecognitionIntervalMs: Number(sp('faceRecognitionIntervalMs', 'FaceRecognitionIntervalMs') ?? 2000),
        qrVerificationMode: String(sp('qrVerificationMode', 'QrVerificationMode') ?? defaults.qrVerificationMode),
        photoDisplay: String(sp('photoDisplay', 'PhotoDisplay') ?? defaults.photoDisplay),
        pushConfigToDevice: Boolean(sp('pushConfigToDevice', 'PushConfigToDevice') ?? true)
      }
    };
  }

  private detailToForm(detail: FaceDeviceDetailDto): DeviceForm {
    return {
      name: detail.name,
      description: detail.description ?? '',
      deviceIp: detail.deviceIp,
      devicePassword: detail.devicePassword ?? '',
      sortOrder: detail.sortOrder,
      isActive: detail.isActive,
      settings: { ...detail.settings }
    };
  }

  cancelEdit(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.probeResult = null;
    this.message = '';
    this.activeTab = 'details';
    this.cdr.markForCheck();
  }

  probeDevice(): void {
    const ip = this.form.deviceIp.trim();
    if (!ip) {
      this.setMessage('Enter a device IP before probing.', 'err');
      return;
    }

    this.probing = true;
    this.probeResult = null;
    this.message = '';
    this.api.probeFaceDevice(ip).subscribe({
      next: (res) => {
        this.probeResult = this.normalizeProbe(res);
        this.probing = false;
        if (this.probeResult.reachable) {
          this.setMessage('Terminal responded. You can register this device.', 'ok');
        } else {
          this.setMessage(this.probeResult.detail ?? 'Terminal did not respond.', 'err');
        }
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.probing = false;
        this.setMessage(this.readError(err, 'Probe failed.'), 'err');
        this.cdr.markForCheck();
      }
    });
  }

  saveDevice(): void {
    const name = this.form.name.trim();
    const ip = this.form.deviceIp.trim();
    if (!name || !ip) {
      this.setMessage('Device name and IP address are required.', 'err');
      return;
    }
    if (name.length > 30) {
      this.setMessage('Device name must be at most 30 characters.', 'err');
      return;
    }

    this.saving = true;
    this.message = '';
    const payload: SaveFaceDeviceRequest = {
      name,
      description: this.form.description?.trim() || null,
      deviceIp: ip,
      devicePassword: this.form.devicePassword?.trim() || null,
      sortOrder: this.form.sortOrder,
      isActive: this.form.isActive,
      settings: { ...this.form.settings }
    };

    this.api.saveFaceDevice(payload, this.editingId).subscribe({
      next: (res) => {
        const normalized = this.normalizeSaveResult(res);
        const row = this.detailToListRow(normalized.device);
        if (this.editingId == null) {
          this.devices = [...this.devices, row].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
        } else {
          this.devices = this.devices.map((d) => (d.id === row.id ? row : d));
        }
        this.saving = false;
        this.editingId = null;
        this.form = this.emptyForm();
        this.probeResult = null;
        if (normalized.configPushWarning) {
          this.setMessage(`Device saved. ${normalized.configPushWarning}`, 'info');
        } else if (normalized.configPushedToDevice) {
          this.setMessage(`Device "${row.name}" saved and settings pushed to terminal.`, 'ok');
        } else {
          this.setMessage(`Device "${row.name}" saved.`, 'ok');
        }
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.saving = false;
        this.setMessage(this.readError(err, 'Save failed.'), 'err');
        this.cdr.markForCheck();
      }
    });
  }

  private detailToListRow(detail: FaceDeviceDetailDto): FaceDeviceListDto {
    return {
      id: detail.id,
      name: detail.name,
      siteControl: detail.settings.siteControl ?? null,
      deviceIp: detail.deviceIp,
      direction: detail.settings.direction,
      isActive: detail.isActive,
      sortOrder: detail.sortOrder
    };
  }

  private normalizeSaveResult(raw: Record<string, unknown> | { device: FaceDeviceDetailDto }): {
    device: FaceDeviceDetailDto;
    configPushedToDevice: boolean;
    configPushWarning?: string | null;
  } {
    const r = raw as Record<string, unknown>;
    const pick = (c: string, p: string) => r[c] ?? r[p];
    const deviceRaw = pick('device', 'Device');
    return {
      device: this.normalizeDetail(deviceRaw as FaceDeviceDetailDto),
      configPushedToDevice: Boolean(pick('configPushedToDevice', 'ConfigPushedToDevice')),
      configPushWarning: (pick('configPushWarning', 'ConfigPushWarning') as string | null | undefined) ?? null
    };
  }

  private normalizeProbe(raw: FaceDeviceProbeDto | Record<string, unknown>): FaceDeviceProbeDto {
    const r = raw as Record<string, unknown>;
    const pick = (c: string, p: string) => r[c] ?? r[p];
    return {
      reachable: Boolean(pick('reachable', 'Reachable')),
      deviceKey: (pick('deviceKey', 'DeviceKey') as string | null | undefined) ?? null,
      detail: (pick('detail', 'Detail') as string | null | undefined) ?? null
    };
  }

  private readError(err: { error?: unknown }, fallback: string): string {
    const payload = err?.error;
    if (payload && typeof payload === 'object' && typeof (payload as { message?: string }).message === 'string') {
      return (payload as { message: string }).message;
    }
    return fallback;
  }

  private setMessage(text: string, tone: 'ok' | 'err' | 'info'): void {
    this.message = text;
    this.messageTone = tone;
  }
}
