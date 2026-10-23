import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { apiError } from '../../../core/utils';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  demoAccounts = [
    { role: 'Patient', email: 'patient@clinic.com', password: 'Patient@123' },
    { role: 'Doctor', email: 'doctor@clinic.com', password: 'Doctor@123' },
    { role: 'Admin', email: 'admin@clinic.com', password: 'Admin@123' }
  ];

  useDemo(account: { email: string; password: string }) {
    this.form.setValue({ email: account.email, password: account.password });
    this.error.set(null);
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: res => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(returnUrl || this.auth.homeRoute(res.user.role));
      },
      error: err => {
        this.error.set(apiError(err, 'Sign-in failed. Check your email and password.'));
        this.loading.set(false);
      }
    });
  }
}
