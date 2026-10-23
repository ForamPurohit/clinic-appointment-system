import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Specialization } from '../../core/models/models';
import { AuthService } from '../../core/services/auth.service';
import { SpecializationService } from '../../core/services/specialization.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './home.component.html'
})
export class HomeComponent implements OnInit {
  auth = inject(AuthService);
  private specializationService = inject(SpecializationService);

  specializations = signal<Specialization[]>([]);

  // Static preview of the slot picker shown in the hero
  previewSlots = [
    { time: '9:00', open: false },
    { time: '9:30', open: true },
    { time: '10:00', open: true },
    { time: '10:30', open: false },
    { time: '11:00', open: true },
    { time: '11:30', open: true }
  ];

  ngOnInit() {
    this.specializationService.getAll().subscribe({
      next: list => this.specializations.set(list),
      error: () => this.specializations.set([])
    });
  }

  get bookLink(): string {
    const user = this.auth.user();
    if (!user) return '/register';
    return user.role === 'Patient' ? '/patient/book' : this.auth.homeRoute(user.role);
  }
}
