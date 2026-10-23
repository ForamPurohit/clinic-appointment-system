import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

interface NavLink {
  label: string;
  path: string;
}

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './navbar.component.html'
})
export class NavbarComponent {
  auth = inject(AuthService);
  menuOpen = signal(false);

  links = computed<NavLink[]>(() => {
    switch (this.auth.user()?.role) {
      case 'Patient':
        return [
          { label: 'My appointments', path: '/patient/appointments' },
          { label: 'Book a visit', path: '/patient/book' }
        ];
      case 'Doctor':
        return [
          { label: 'My schedule', path: '/doctor/schedule' },
          { label: 'Time off', path: '/doctor/leave' }
        ];
      case 'Admin':
        return [
          { label: 'Overview', path: '/admin/dashboard' },
          { label: 'Appointments', path: '/admin/appointments' },
          { label: 'Doctors', path: '/admin/doctors' },
          { label: 'Specialties', path: '/admin/specializations' },
          { label: 'Patients', path: '/admin/patients' }
        ];
      default:
        return [];
    }
  });

  initials = computed(() =>
    (this.auth.user()?.fullName ?? '')
      .replace(/^Dr\.?\s+/i, '')
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0].toUpperCase())
      .join('')
  );

  toggleMenu() {
    this.menuOpen.update(open => !open);
  }

  closeMenu() {
    this.menuOpen.set(false);
  }
}
