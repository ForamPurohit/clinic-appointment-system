import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { apiError } from '../../../core/utils';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', Validators.pattern(/^[0-9+\-\s]{7,20}$/)],
    dateOfBirth: [''],
    gender: [''],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    const v = this.form.getRawValue();
    this.auth.register({
      fullName: v.fullName,
      email: v.email,
      password: v.password,
      phoneNumber: v.phoneNumber || null,
      dateOfBirth: v.dateOfBirth || null,
      gender: v.gender || null
    }).subscribe({
      next: () => this.router.navigate(['/patient/book']),
      error: err => {
        this.error.set(apiError(err, "Couldn't create the account. Try again."));
        this.loading.set(false);
      }
    });
  }
}
