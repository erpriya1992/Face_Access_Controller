import { CommonModule } from '@angular/common';
import { Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { BlazeFaceModel, NormalizedFace } from '@tensorflow-models/blazeface';
import * as blazeface from '@tensorflow-models/blazeface';
import { ApiService, DeviceConnectivityStatus, FaceDeviceListDto } from '../../api.service';

/** Face scan UI state for auto-capture (BlazeFace + oval guide). */
export type FaceScanStatus = 'off' | 'loading' | 'scanning' | 'align' | 'hold' | 'done' | 'error';

export type BannerTone = 'info' | 'success' | 'warn' | 'error';

export interface UiBanner {
  tone: BannerTone;
  title: string;
  detail?: string;
  /** Offer status refresh when gateway or terminal is down */
  showConnectionRetry?: boolean;
}

@Component({
  selector: 'app-registration',
  imports: [CommonModule, FormsModule],
  templateUrl: './registration.component.html',
  styleUrl: './registration.component.scss'
})
export class RegistrationComponent implements OnInit, OnDestroy {
  @ViewChild('cameraVideo') cameraVideo?: ElementRef<HTMLVideoElement>;
  @ViewChild('captureCanvas') captureCanvas?: ElementRef<HTMLCanvasElement>;

  registerForm: {
    personId: string;
    fullName: string;
    idCardNumber: string;
    phone: string;
    department: string;
    imageBase64: string;
  } = {
    personId: '',
    fullName: '',
    idCardNumber: '',
    phone: '',
    department: '',
    imageBase64: ''
  };

  /** faceDeviceId -> grant access / enroll on this reader */
  deviceAccess: Record<number, boolean> = {};

  /** All devices from API; enrollment dropdown uses active only. */
  faceDevices: FaceDeviceListDto[] = [];
  faceDevicesLoaded = false;
  cameraOn = false;
  capturedPreview = '';
  /** Auto face-detect pipeline status (shown under the camera frame). */
  faceScanStatus: FaceScanStatus = 'off';
  banner: UiBanner | null = null;

  deviceStatus: DeviceConnectivityStatus | null = null;
  statusLoading = false;

  /** Modal shown when the server reports duplicate enrollment (database or face device). */
  duplicateDialog: { title: string; body: string } | null = null;
  submitInFlight = false;

  private cameraStream?: MediaStream;
  private statusPoll?: ReturnType<typeof setInterval>;
  private blazefaceModel: BlazeFaceModel | null = null;
  private faceDetectRaf = 0;
  private lastFaceDetectMs = 0;
  private stableFaceFrames = 0;
  /** Fewer frames = faster capture when conditions are stable. */
  private readonly stableFramesNeeded = 5;
  private readonly detectIntervalMs = 90;
  private autoCaptureComplete = false;
  /** Ensures we only bootstrap the ML loop once per camera session. */
  private faceDetectionBootstrapped = false;
  /** Alternate webcam flip — some browsers/devices need false instead of true. */
  private flipHorizontal = true;
  private flipProbeCounter = 0;

  constructor(
    private api: ApiService,
    private ngZone: NgZone
  ) {}

  get faceScanHint(): string {
    switch (this.faceScanStatus) {
      case 'loading':
        return 'Loading face detector…';
      case 'scanning':
        return 'Position your face in the oval — capture starts automatically.';
      case 'align':
        return 'Center your face in the oval and move a little closer.';
      case 'hold':
        return 'Hold steady…';
      case 'done':
        return 'Face captured.';
      case 'error':
        return 'Auto-capture is unavailable. Use “Manual capture” below.';
      default:
        return '';
    }
  }

  get submitBlocked(): boolean {
    if (this.statusLoading) {
      return true;
    }
    if (!this.deviceStatus) {
      return true;
    }
    if (!this.deviceStatus.middlewareOnline || !this.deviceStatus.deviceOnline) {
      return true;
    }
    const personId = (this.registerForm.personId ?? '').trim();
    const fullName = (this.registerForm.fullName ?? '').trim();
    if (!personId || !fullName) {
      return true;
    }
    if (!this.isFullNamePatternOk(fullName)) {
      return true;
    }
    if (!this.registerForm.imageBase64?.trim()) {
      return true;
    }
    const active = this.activeFaceDevices;
    if (active.length > 0 && this.selectedDeviceCount === 0) {
      return true;
    }
    return false;
  }

  get selectedDeviceCount(): number {
    return this.activeFaceDevices.filter((d) => this.deviceAccess[d.id]).length;
  }

  get activeFaceDevices(): FaceDeviceListDto[] {
    return this.faceDevices.filter((d) => d.isActive);
  }

  /** Same rule as validateIndianNameOrReset — keeps Submit disabled until the name is acceptable. */
  private isFullNamePatternOk(fullName: string): boolean {
    return /^[A-Za-z]+(?:\s+[A-Za-z]+)+$/.test(fullName.trim());
  }

  ngOnInit(): void {
    this.loadDeviceStatus();
    this.loadFaceDevices();
    this.statusPoll = setInterval(() => this.loadDeviceStatus(), 30000);
  }

  loadFaceDevices(): void {
    this.api.getFaceDevices().subscribe({
      next: (rows) => {
        this.faceDevices = (rows ?? []).map((r) => this.normalizeFaceDevice(r));
        this.faceDevicesLoaded = true;
        this.applyDefaultFaceDeviceSelection();
      },
      error: () => {
        this.faceDevices = [];
        this.faceDevicesLoaded = true;
      }
    });
  }

  private normalizeFaceDevice(raw: FaceDeviceListDto | Record<string, unknown>): FaceDeviceListDto {
    const r = raw as Record<string, unknown>;
    const pick = (c: string, p: string) => r[c] ?? r[p];
    return {
      id: Number(pick('id', 'Id') ?? 0),
      name: String(pick('name', 'Name') ?? ''),
      siteControl: (pick('siteControl', 'SiteControl') as string | null | undefined) ?? null,
      deviceIp: String(pick('deviceIp', 'DeviceIp') ?? ''),
      direction: String(pick('direction', 'Direction') ?? 'Entry'),
      isActive: Boolean(pick('isActive', 'IsActive')),
      sortOrder: Number(pick('sortOrder', 'SortOrder') ?? 0)
    };
  }

  private applyDefaultFaceDeviceSelection(): void {
    const active = this.activeFaceDevices;
    const next: Record<number, boolean> = {};
    active.forEach((d) => {
      next[d.id] = active.length === 1;
    });
    this.deviceAccess = next;
  }

  toggleDeviceAccess(deviceId: number, allowed: boolean): void {
    this.deviceAccess = { ...this.deviceAccess, [deviceId]: allowed };
  }

  isDeviceAccessChecked(deviceId: number): boolean {
    return !!this.deviceAccess[deviceId];
  }

  loadDeviceStatus(): void {
    this.statusLoading = true;
    this.api.getDeviceStatus().subscribe({
      next: (s) => {
        this.deviceStatus = s;
        this.statusLoading = false;
        if (!s.middlewareOnline) {
          this.setBanner(
            'error',
            'Gateway host offline',
            'No TCP/HTTP response from the middleware base URL. Confirm `FacereaderMiddleware:BaseUrl` / `ExternalApis:PersonApiBaseUrl` and that the middleware process is listening.',
            true
          );
        } else if (!s.deviceOnline) {
          this.setBanner(
            'warn',
            'Terminal not responding',
            'Gateway is up; the record pull from the terminal failed. Check device power, LAN, `DeviceIp`, and `DevicePassword` in configuration.',
            true
          );
        } else if (this.banner?.showConnectionRetry) {
          this.banner = null;
        }
      },
      error: () => {
        this.deviceStatus = null;
        this.statusLoading = false;
        this.setBanner(
          'error',
          'Status request failed',
          'The browser did not receive a valid JSON response (session, CORS, or API down). Re-authenticate, then confirm `apiBaseUrl` in `assets/app-config.json` matches the running API host.',
          true
        );
      }
    });
  }

  formatStatusTime(iso: string): string {
    try {
      return new Date(iso).toLocaleString(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short'
      });
    } catch {
      return iso;
    }
  }

  dismissBanner(): void {
    this.banner = null;
  }

  closeDuplicateDialog(): void {
    this.duplicateDialog = null;
  }

  private duplicateConflictUi(code: string | undefined, serverMessage: string): void {
    const byCode: Record<string, { title: string; body: string }> = {
      duplicate_in_database: {
        title: 'Already registered in database',
        body:
          'This Person ID already exists in the system. Use a different ID or ask an administrator to update the existing record.'
      },
      duplicate_card_in_database: {
        title: 'Card already assigned',
        body:
          'This card number already belongs to another employee in the database. Use a different card number or update the existing employee record.'
      },
      duplicate_on_device: {
        title: 'Face already on terminal',
        body:
          'This Person ID is already enrolled on the face reader. Your photo may already be registered for this ID. If you need a new enrollment, contact an administrator.'
      },
      duplicate_card_on_device: {
        title: 'Card already on controller',
        body:
          'This card number is already registered on the selected controller. Use a different card number or remove the old card entry first.'
      }
    };
    const preset = code ? byCode[code] : undefined;
    this.duplicateDialog = preset ?? {
      title: 'Already registered',
      body: serverMessage || 'This Person ID appears to be registered already.'
    };
  }

  private setBanner(tone: BannerTone, title: string, detail?: string, showConnectionRetry?: boolean): void {
    this.banner = { tone, title, detail, showConnectionRetry };
  }

  async startCamera(): Promise<void> {
    try {
      this.cancelFaceDetectionLoop();
      this.autoCaptureComplete = false;
      this.stableFaceFrames = 0;
      this.faceDetectionBootstrapped = false;
      this.flipHorizontal = true;
      this.flipProbeCounter = 0;
      this.faceScanStatus = 'off';
      this.capturedPreview = '';
      this.registerForm.imageBase64 = '';

      this.cameraStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false
      });
      const video = this.cameraVideo?.nativeElement;
      if (video) {
        video.srcObject = this.cameraStream;
        this.cameraOn = true;
        this.faceScanStatus = 'loading';
        this.setBanner(
          'info',
          'Camera running',
          'Stay in the oval — the photo is taken automatically when your face is detected.'
        );

        const tryStart = () => {
          if (this.autoCaptureComplete || this.registerForm.imageBase64?.trim()) {
            return;
          }
          if (this.faceDetectionBootstrapped) {
            return;
          }
          this.faceDetectionBootstrapped = true;
          void this.ensureBlazeFaceAndStartLoop();
        };

        video.onloadeddata = () => this.ngZone.run(() => tryStart());
        video.onplaying = () => this.ngZone.run(() => tryStart());
        video.oncanplay = () => this.ngZone.run(() => tryStart());

        try {
          await video.play();
        } catch {
          /* play() can fail until metadata; events above still fire */
        }

        // Fallback if events do not fire (some browsers / policies)
        setTimeout(() => this.ngZone.run(() => tryStart()), 300);
        setTimeout(() => this.ngZone.run(() => tryStart()), 800);
      }
    } catch {
      this.faceScanStatus = 'off';
      this.setBanner(
        'warn',
        'Camera blocked',
        'Enable camera permission for this origin in the browser, then Open camera again.'
      );
    }
  }

  stopCamera(): void {
    this.cancelFaceDetectionLoop();
    this.faceDetectionBootstrapped = false;
    this.faceScanStatus = 'off';
    this.blazefaceModel?.dispose();
    this.blazefaceModel = null;
    this.cameraStream?.getTracks().forEach((t) => t.stop());
    this.cameraStream = undefined;
    this.cameraOn = false;
  }

  /** Manual fallback if auto-capture fails or user prefers to trigger capture. */
  captureFaceManual(): void {
    this.captureFace(false);
  }

  /**
   * Grabs the current video frame into the registration payload.
   * @param fromAuto when true, banner copy reflects automatic capture.
   */
  captureFace(fromAuto = true): void {
    const video = this.cameraVideo?.nativeElement;
    const canvas = this.captureCanvas?.nativeElement;
    if (!video || !canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    this.cancelFaceDetectionLoop();

    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    const dataUrl = canvas.toDataURL('image/jpeg', 0.92);

    const detail = fromAuto
      ? 'Captured automatically. Submit when gateway and terminal are online.'
      : 'Submit registration when both device status lines show Online.';

    this.ngZone.run(() => {
      this.autoCaptureComplete = true;
      this.faceScanStatus = 'done';
      this.capturedPreview = dataUrl;
      this.registerForm.imageBase64 = dataUrl;
      this.setBanner('success', 'Frame stored', detail);
    });
  }

  private cancelFaceDetectionLoop(): void {
    if (this.faceDetectRaf) {
      cancelAnimationFrame(this.faceDetectRaf);
      this.faceDetectRaf = 0;
    }
  }

  private async ensureBlazeFaceAndStartLoop(): Promise<void> {
    if (this.autoCaptureComplete || this.registerForm.imageBase64?.trim()) {
      return;
    }
    try {
      if (!this.blazefaceModel) {
        this.ngZone.run(() => {
          this.faceScanStatus = 'loading';
        });
        this.blazefaceModel = await blazeface.load({ maxFaces: 1, scoreThreshold: 0.5 });
      }
      this.ngZone.run(() => {
        this.faceScanStatus = 'scanning';
      });
      this.lastFaceDetectMs = 0;
      this.stableFaceFrames = 0;
      void this.runFaceDetectionLoop();
    } catch {
      this.faceDetectionBootstrapped = false;
      this.ngZone.run(() => {
        this.faceScanStatus = 'error';
        this.setBanner(
          'warn',
          'Face detection unavailable',
          'Your browser could not load the face detector. Use “Manual capture” to take a photo.'
        );
      });
    }
  }

  private async runFaceDetectionLoop(): Promise<void> {
    if (!this.cameraOn || this.autoCaptureComplete) {
      return;
    }
    if (this.registerForm.imageBase64?.trim()) {
      return;
    }

    const video = this.cameraVideo?.nativeElement;
    if (!video || video.readyState < 2) {
      this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
      return;
    }
    if (!this.blazefaceModel) {
      return;
    }

    const now = performance.now();
    if (now - this.lastFaceDetectMs < this.detectIntervalMs) {
      this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
      return;
    }
    this.lastFaceDetectMs = now;

    let predictions: NormalizedFace[];
    try {
      predictions = await this.blazefaceModel.estimateFaces(video, false, this.flipHorizontal);
    } catch {
      this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
      return;
    }

    if (!predictions.length) {
      this.flipProbeCounter++;
      if (this.flipProbeCounter > 25) {
        this.flipProbeCounter = 0;
        this.flipHorizontal = !this.flipHorizontal;
      }
      this.stableFaceFrames = 0;
      this.ngZone.run(() => {
        this.faceScanStatus = 'scanning';
      });
      this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
      return;
    }

    this.flipProbeCounter = 0;

    const face = predictions[0];
    const tl = face.topLeft;
    const br = face.bottomRight;
    const coords = this.readFaceBox(tl, br);
    if (!coords) {
      this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
      return;
    }

    const prob = this.readFaceProbability(face);
    if (prob < 0.38) {
      this.stableFaceFrames = 0;
      this.ngZone.run(() => {
        this.faceScanStatus = 'scanning';
      });
      this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
      return;
    }

    const fx = (coords.tl[0] + coords.br[0]) / 2;
    const fy = (coords.tl[1] + coords.br[1]) / 2;
    const fw = Math.abs(coords.br[0] - coords.tl[0]);
    const fh = Math.abs(coords.br[1] - coords.tl[1]);

    const { x: nx, y: ny } = this.faceCenterInFrameNorm(fx, fy, video);
    const faceWNorm = this.faceSizeNorm(fw, video);
    const faceHNorm = this.faceSizeNorm(fh, video);

    // Face large enough for enrollment (relaxed so auto-capture actually fires in real lighting).
    const largeEnough = faceWNorm >= 0.048 && faceHNorm >= 0.055;
    const inGuide = this.isFaceCenterInGuide(nx, ny);
    const readyToCount = largeEnough;

    if (readyToCount) {
      this.stableFaceFrames++;
      this.ngZone.run(() => {
        this.faceScanStatus = 'hold';
      });
      if (this.stableFaceFrames >= this.stableFramesNeeded) {
        this.captureFace(true);
        return;
      }
    } else {
      this.stableFaceFrames = 0;
      this.ngZone.run(() => {
        if (largeEnough && !inGuide) {
          this.faceScanStatus = 'align';
        } else {
          this.faceScanStatus = 'scanning';
        }
      });
    }

    this.faceDetectRaf = requestAnimationFrame(() => void this.runFaceDetectionLoop());
  }

  /** BlazeFace may return plain arrays or nested arrays for probability. */
  private readFaceProbability(face: NormalizedFace): number {
    const p = face.probability as unknown;
    if (typeof p === 'number' && !Number.isNaN(p)) {
      return p;
    }
    if (p && typeof p === 'object' && 'dataSync' in p && typeof (p as { dataSync: () => Float32Array }).dataSync === 'function') {
      const d = (p as { dataSync: () => Float32Array }).dataSync();
      return d.length ? d[0] : 0;
    }
    if (Array.isArray(p)) {
      const flat = p.flat(2) as number[];
      for (const v of flat) {
        if (typeof v === 'number' && v > 0 && v <= 1) {
          return v;
        }
      }
    }
    return 0.75;
  }

  private readFaceBox(
    tl: NormalizedFace['topLeft'],
    br: NormalizedFace['bottomRight']
  ): { tl: [number, number]; br: [number, number] } | null {
    const a = this.toNumPair(tl);
    const b = this.toNumPair(br);
    if (!a || !b) {
      return null;
    }
    return { tl: a, br: b };
  }

  private toNumPair(v: unknown): [number, number] | null {
    if (Array.isArray(v) && v.length >= 2 && typeof v[0] === 'number' && typeof v[1] === 'number') {
      return [v[0], v[1]];
    }
    if (v && typeof v === 'object' && 'dataSync' in v && typeof (v as { dataSync: () => Float32Array }).dataSync === 'function') {
      const d = (v as { dataSync: () => Float32Array }).dataSync();
      if (d.length >= 2) {
        return [d[0], d[1]];
      }
    }
    return null;
  }

  /**
   * Maps a point from video pixel space to normalized [0,1] coordinates in the element box,
   * accounting for CSS object-fit: cover (same as the on-screen oval guide).
   */
  private faceCenterInFrameNorm(fx: number, fy: number, video: HTMLVideoElement): { x: number; y: number } {
    const vw = video.videoWidth;
    const vh = video.videoHeight;
    const rw = video.clientWidth;
    const rh = video.clientHeight;
    if (!vw || !vh || !rw || !rh) {
      return { x: 0.5, y: 0.5 };
    }
    const scale = Math.max(rw / vw, rh / vh);
    const dispW = vw * scale;
    const dispH = vh * scale;
    const offX = (rw - dispW) / 2;
    const offY = (rh - dispH) / 2;
    const sx = offX + fx * scale;
    const sy = offY + fy * scale;
    return { x: sx / rw, y: sy / rh };
  }

  private faceSizeNorm(dimPx: number, video: HTMLVideoElement): number {
    const vw = video.videoWidth;
    const vh = video.videoHeight;
    const rw = video.clientWidth;
    const rh = video.clientHeight;
    if (!vw || !vh || !rw || !rh) return 0;
    const scale = Math.max(rw / vw, rh / vh);
    return (dimPx * scale) / rw;
  }

  /** Matches `.face-guide` in SCSS: left 28%, top 14%, width 44%, height 68% → ellipse center ~ (0.5, 0.48). */
  private isFaceCenterInGuide(nx: number, ny: number): boolean {
    const cx = 0.5;
    const cy = 0.14 + 0.68 / 2;
    const rx = 0.44 / 2;
    const ry = 0.68 / 2;
    const dx = (nx - cx) / rx;
    const dy = (ny - cy) / ry;
    return dx * dx + dy * dy <= 1.08;
  }

  registerFace(): void {
    if (this.submitBlocked) {
      if (this.statusLoading || !this.deviceStatus || !this.deviceStatus.middlewareOnline || !this.deviceStatus.deviceOnline) {
        this.setBanner(
          'warn',
          'Device status not ready',
          'Clear the gateway/terminal faults above, or press Refresh status.',
          true
        );
      } else if (!(this.registerForm.personId ?? '').trim() || !(this.registerForm.fullName ?? '').trim()) {
        this.setBanner('info', 'Required fields', 'Enter Person ID and full name before submitting.');
      } else if (!this.isFullNamePatternOk((this.registerForm.fullName ?? '').trim())) {
        this.setBanner(
          'warn',
          'Invalid full name',
          'Use letters and spaces only; minimum first name plus surname (e.g. Ravi Kumar).'
        );
      } else if (!this.registerForm.imageBase64?.trim()) {
        this.setBanner('info', 'Image missing', 'Open Face capture and let the camera save your photo automatically (or use Manual capture).');
      } else if (this.activeFaceDevices.length > 0 && this.selectedDeviceCount === 0) {
        this.setBanner(
          'info',
          'Select face readers',
          'Check at least one face reader to enroll this employee on.'
        );
      }
      return;
    }

    if (!this.validateIndianNameOrReset()) {
      return;
    }

    this.banner = null;
    this.duplicateDialog = null;
    this.submitInFlight = true;

    this.api
      .registerFace({
        ...this.registerForm,
        faceDeviceAccess: this.activeFaceDevices.map((d) => ({
          faceDeviceId: d.id,
          accessAllowed: !!this.deviceAccess[d.id]
        }))
      })
      .subscribe({
      next: (res) => {
        this.submitInFlight = false;
        const base =
          res?.message?.trim() ||
          'The middleware returned success for the person record and face image. Add another user or clear the form.';
        const uiNote =
          res?.faceDeviceUiApplied === false
            ? ' Custom terminal message was not applied: check that FaceReader_Middleware is running, MainGate device URL matches your reader, and PersonApiPass matches the device password (see API logs).'
            : '';
        this.setBanner('success', 'Registration accepted', base + uiNote);
      },
      error: (err) => {
        this.submitInFlight = false;
        const status = err?.status as number | undefined;
        const payload = err?.error;
        const apiMessage =
          (payload && typeof payload === 'object' && (payload as { message?: string }).message) ||
          (typeof payload === 'string' ? payload : '') ||
          (err?.message ? err.message : '');
        const apiDetail =
          payload && typeof payload === 'object' ? (payload as { detail?: string }).detail : undefined;
        const code =
          payload && typeof payload === 'object' ? (payload as { code?: string }).code : undefined;

        if (status === 409) {
          this.duplicateConflictUi(code, typeof apiMessage === 'string' ? apiMessage : '');
          this.setBanner(
            'warn',
            'Duplicate enrollment',
            typeof apiMessage === 'string' ? apiMessage : 'This Person ID is already registered.'
          );
          return;
        }

        if (status === 502 && this.isLikelyDuplicateCardDetail(apiDetail)) {
          this.duplicateConflictUi('duplicate_card_on_device', '');
          this.setBanner(
            'warn',
            'Duplicate card on controller',
            'This card number appears to already exist on the selected controller.'
          );
          return;
        }

        if (status === 400 && typeof apiMessage === 'string' && apiMessage.toLowerCase().includes('face device')) {
          this.setBanner('warn', 'Face reader', apiMessage);
          return;
        }

        this.setBanner(
          'error',
          'Registration rejected',
          apiMessage ||
            'Non-success from person or photo step (duplicate PersonId, bad image, or device firmware error). Inspect the JSON error from the API and middleware trace logs.'
        );
      }
    });
  }

  private isLikelyDuplicateCardDetail(detail: unknown): boolean {
    if (typeof detail !== 'string' || !detail.trim()) {
      return false;
    }

    const text = detail.toLowerCase();
    const mentionsCard = text.includes('card') || text.includes('idcard') || text.includes('id card');
    const mentionsDuplicate = text.includes('already') || text.includes('exist') || text.includes('duplicate');
    return mentionsCard && mentionsDuplicate;
  }

  ngOnDestroy(): void {
    if (this.statusPoll) {
      clearInterval(this.statusPoll);
    }
    this.stopCamera();
  }

  validateIndianNameOrReset(): boolean {
    const fullName = (this.registerForm.fullName ?? '').trim();
    const indianNamePattern = /^[A-Za-z]+(?:\s+[A-Za-z]+)+$/;

    if (!indianNamePattern.test(fullName)) {
      this.resetFormFields();
      this.setBanner(
        'warn',
        'Invalid full name',
        'Use letters and spaces only; minimum first name plus surname (e.g. Ravi Kumar).'
      );
      return false;
    }

    return true;
  }

  private resetFormFields(): void {
    this.cancelFaceDetectionLoop();
    this.autoCaptureComplete = false;
    this.faceScanStatus = 'off';
    this.registerForm.personId = '';
    this.registerForm.fullName = '';
    this.registerForm.idCardNumber = '';
    this.registerForm.phone = '';
    this.registerForm.department = '';
    this.registerForm.imageBase64 = '';
    this.capturedPreview = '';
  }
}
