import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from './api.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  constructor(private api: ApiService, private router: Router) {}

  get showLayout(): boolean {
    return !this.router.url.startsWith('/login');
  }

  logout(): void {
    this.api.clearToken();
    this.router.navigate(['/login']);
  }
}
