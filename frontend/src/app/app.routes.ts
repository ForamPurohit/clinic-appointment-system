import { Routes } from '@angular/router';
import { guestGuard, roleGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'patient',
    canActivate: [roleGuard],
    data: { roles: ['Patient'] },
    children: [
      {
        path: 'book',
        loadComponent: () =>
          import('./features/patient/book-appointment/book-appointment.component').then(m => m.BookAppointmentComponent)
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/patient/my-appointments/my-appointments.component').then(m => m.MyAppointmentsComponent)
      },
      { path: '', redirectTo: 'appointments', pathMatch: 'full' }
    ]
  },
  {
    path: 'reschedule/:id',
    canActivate: [roleGuard],
    data: { roles: ['Patient', 'Admin'] },
    loadComponent: () => import('./features/patient/reschedule/reschedule.component').then(m => m.RescheduleComponent)
  },
  {
    path: 'doctor',
    canActivate: [roleGuard],
    data: { roles: ['Doctor'] },
    children: [
      {
        path: 'schedule',
        loadComponent: () => import('./features/doctor/schedule/schedule.component').then(m => m.ScheduleComponent)
      },
      {
        path: 'leave',
        loadComponent: () => import('./features/doctor/leave/leave.component').then(m => m.LeaveComponent)
      },
      { path: '', redirectTo: 'schedule', pathMatch: 'full' }
    ]
  },
  {
    path: 'admin',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/admin/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'doctors',
        loadComponent: () => import('./features/admin/doctors/doctors.component').then(m => m.DoctorsComponent)
      },
      {
        path: 'specializations',
        loadComponent: () =>
          import('./features/admin/specializations/specializations.component').then(m => m.SpecializationsComponent)
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/admin/appointments/appointments.component').then(m => m.AdminAppointmentsComponent)
      },
      {
        path: 'patients',
        loadComponent: () => import('./features/admin/patients/patients.component').then(m => m.PatientsComponent)
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: '' }
];
