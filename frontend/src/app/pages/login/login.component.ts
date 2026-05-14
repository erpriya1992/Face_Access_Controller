import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../api.service';

@Component({
  selector: 'app-login',
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  username = 'admin';
  password = 'Admin@123';
  message = '';
  loading = false;

  constructor(private api: ApiService, private router: Router) {}

  ngOnInit(): void {
    // Fetch admin credentials from backend config (password only returned in Development).
    this.api.getAdminConfig().subscribe({
      next: (cfg) => {
        if (cfg?.username) this.username = cfg.username;
        if (cfg?.password) this.password = cfg.password;
      },
      error: () => {
        // Keep defaults if config endpoint is not available.
      }
    });
  }

  login(): void {
    this.message = '';
    this.loading = true;
    this.api.login(this.username, this.password).subscribe({
      next: (res) => {
        this.api.setToken(res.token);
        this.message = `Welcome ${res.username}`;
        this.loading = false;
        this.router.navigate(['/registration']);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 0 || error.status === 404) {
          this.message = 'Login failed. API not found.';
        } else {
          this.message = 'Login failed.';
        }
        this.loading = false;
      }
    });
  }
}
