import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Appointment, AppointmentFilter, Doctor } from '../../../core/models/models';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { apiError } from '../../../core/utils';
import { StatusBadgeComponent } from '../../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-admin-appointments',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, StatusBadgeComponent],
  templateUrl: './appointments.component.html'
})
export class AdminAppointmentsComponent implements OnInit {
  private appointmentService = inject(AppointmentService);
  private doctorService = inject(DoctorService);

  appointments = signal<Appointment[]>([]);
  doctors = signal<Doctor[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  filter: AppointmentFilter = { status: '', doctorId: null, date: '' };
  statuses = ['Scheduled', 'Completed', 'Cancelled', 'NoShow'];

  ngOnInit() {
    this.doctorService.getAll(null, true).subscribe(list => this.doctors.set(list));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.appointmentService.getAll(this.filter).subscribe({
      next: list => {
        this.appointments.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(apiError(err));
        this.loading.set(false);
      }
    });
  }

  clearFilters() {
    this.filter = { status: '', doctorId: null, date: '' };
    this.load();
  }

  cancel(a: Appointment) {
    if (!confirm(`Cancel ${a.patientName}'s appointment with ${a.doctorName}?`)) return;
    this.appointmentService.cancel(a.id).subscribe({
      next: updated => this.appointments.update(list => list.map(x => (x.id === updated.id ? updated : x))),
      error: err => this.error.set(apiError(err))
    });
  }
}
