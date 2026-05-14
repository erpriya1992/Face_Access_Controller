import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { RegistrationComponent } from './pages/registration/registration.component';
import { LiveComponent } from './pages/live/live.component';
import { ReportsComponent } from './pages/reports/reports.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { DeviceRegistrationComponent } from './pages/device-registration/device-registration.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'registration', component: RegistrationComponent, canActivate: [authGuard] },
  { path: 'device-registration', component: DeviceRegistrationComponent, canActivate: [authGuard] },
  { path: 'live-transactions', component: LiveComponent, canActivate: [authGuard] },
  { path: 'reports', component: ReportsComponent, canActivate: [authGuard] },
  { path: 'settings', component: SettingsComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
