import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Appointment, DashboardStats } from '../../../core/models/models';
import { AdminService } from '../../../core/services/admin.service';
import { AppointmentService } from '../../../core/services/appointment.service';
import { apiError, toIsoDate } from '../../../core/utils';
import { StatusBadgeComponent } from '../../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DatePipe, RouterLink, StatusBadgeComponent],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private adminService = inject(AdminService);
  private appointmentService = inject(AppointmentService);

  stats = signal<DashboardStats | null>(null);
  today = signal<Appointment[]>([]);
  error = signal<string | null>(null);
  now = new Date();

  ngOnInit() {
    this.adminService.getStats().subscribe({
      next: s => this.stats.set(s),
      error: err => this.error.set(apiError(err))
    });

    this.appointmentService.getAll({ date: toIsoDate(this.now) }).subscribe({
      next: list => this.today.set([...list].sort((a, b) => a.startTime.localeCompare(b.startTime))),
      error: err => this.error.set(apiError(err))
    });
  }
}
