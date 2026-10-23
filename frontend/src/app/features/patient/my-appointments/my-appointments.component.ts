import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Appointment, MAX_PATIENT_RESCHEDULES } from '../../../core/models/models';
import { AppointmentService } from '../../../core/services/appointment.service';
import { apiError } from '../../../core/utils';
import { StarsComponent } from '../../../shared/stars/stars.component';
import { StatusBadgeComponent } from '../../../shared/status-badge/status-badge.component';

type Tab = 'upcoming' | 'past';

@Component({
  selector: 'app-my-appointments',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, StatusBadgeComponent, StarsComponent],
  templateUrl: './my-appointments.component.html'
})
export class MyAppointmentsComponent implements OnInit {
  private appointmentService = inject(AppointmentService);
  private route = inject(ActivatedRoute);

  readonly stars = [1, 2, 3, 4, 5];
  readonly reviewWindowDays = 30;

  appointments = signal<Appointment[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  tab = signal<Tab>('upcoming');
  cancellingId = signal<number | null>(null);

  // Review form for one visit at a time
  reviewingId = signal<number | null>(null);
  reviewRating = signal(0);
  reviewComment = '';
  reviewSaving = signal(false);
  reviewError = signal<string | null>(null);

  upcoming = computed(() =>
    this.appointments()
      .filter(a => a.status === 'Scheduled' && new Date(a.startTime) > new Date())
      .sort((a, b) => a.startTime.localeCompare(b.startTime))
  );

  past = computed(() =>
    this.appointments().filter(a => !(a.status === 'Scheduled' && new Date(a.startTime) > new Date()))
  );

  visible = computed(() => (this.tab() === 'upcoming' ? this.upcoming() : this.past()));

  ngOnInit() {
    // Links in review-request emails open the Past tab directly
    if (this.route.snapshot.queryParamMap.get('tab') === 'past') {
      this.tab.set('past');
    }
    this.load();
  }

  canReschedule(a: Appointment): boolean {
    return a.rescheduleCount < MAX_PATIENT_RESCHEDULES;
  }

  canReview(a: Appointment): boolean {
    if (a.status !== 'Completed' || a.rating) return false;
    const ageDays = (Date.now() - new Date(a.startTime).getTime()) / 86_400_000;
    return ageDays <= this.reviewWindowDays;
  }

  openReview(a: Appointment) {
    this.reviewingId.set(a.id);
    this.reviewRating.set(0);
    this.reviewComment = '';
    this.reviewError.set(null);
  }

  submitReview(a: Appointment) {
    if (this.reviewRating() < 1) {
      this.reviewError.set('Choose from 1 to 5 stars.');
      return;
    }

    this.reviewSaving.set(true);
    this.reviewError.set(null);
    this.appointmentService.review(a.id, this.reviewRating(), this.reviewComment.trim() || null).subscribe({
      next: review => {
        this.appointments.update(list => list.map(x => (x.id === a.id ? { ...x, rating: review.rating } : x)));
        this.reviewingId.set(null);
        this.reviewSaving.set(false);
      },
      error: err => {
        this.reviewError.set(apiError(err, "Couldn't save your review."));
        this.reviewSaving.set(false);
      }
    });
  }

  load() {
    this.loading.set(true);
    this.appointmentService.getMine().subscribe({
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

  cancel(appt: Appointment) {
    if (!confirm(`Cancel your appointment with ${appt.doctorName}?`)) return;

    this.cancellingId.set(appt.id);
    this.appointmentService.cancel(appt.id).subscribe({
      next: updated => {
        this.appointments.update(list => list.map(a => (a.id === updated.id ? updated : a)));
        this.cancellingId.set(null);
      },
      error: err => {
        this.error.set(apiError(err));
        this.cancellingId.set(null);
      }
    });
  }
}
