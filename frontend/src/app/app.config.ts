import { APP_INITIALIZER, ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { routes } from './app.routes';
import { AppConfigService } from './app-config.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(routes),
    AppConfigService,
    {
      provide: APP_INITIALIZER,
      useFactory: (cfg: AppConfigService) => () => cfg.load(),
      deps: [AppConfigService],
      multi: true
    }
  ]
};
