import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Appointment, AppointmentStatus, Doctor, ScheduleChangedEvent, WEEKDAYS } from '../../../core/models/models';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { ToastService } from '../../../core/services/toast.service';
import { addDays, apiError, toIsoDate } from '../../../core/utils';
import { StatusBadgeComponent } from '../../../shared/status-badge/status-badge.component';

type FinalStatus = Exclude<AppointmentStatus, 'Scheduled'>;

@Component({
  selector: 'app-schedule',
  standalone: true,
  imports: [DatePipe, FormsModule, StatusBadgeComponent],
  templateUrl: './schedule.component.html'
})
export class ScheduleComponent implements OnInit {
  private appointmentService = inject(AppointmentService);
  private doctorService = inject(DoctorService);
  private realtime = inject(RealtimeService);
  private toast = inject(ToastService);
  private destroyRef = inject(DestroyRef);

  profile = signal<Doctor | null>(null);
  date = signal(toIsoDate(new Date()));
  appointments = signal<Appointment[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  // Notes panel for completing an appointment
  activeId = signal<number | null>(null);
  notes = '';
  savingId = signal<number | null>(null);

  isToday = computed(() => this.date() === toIsoDate(new Date()));
  dateObj = computed(() => new Date(this.date() + 'T00:00:00'));
  scheduledCount = computed(() => this.appointments().filter(a => a.status === 'Scheduled').length);
  hoursLabel = computed(() => {
    const p = this.profile();
    if (!p) return '';
    return `${p.workingDays.map(d => WEEKDAYS[d]).join(', ')}, ${p.workStartTime}–${p.workEndTime}, ${p.slotDurationMinutes}-minute slots`;
  });

  ngOnInit() {
    this.doctorService.getMe().subscribe({
      next: p => this.profile.set(p),
      error: () => this.profile.set(null)
    });
    this.load();

    // Live updates: refresh when patients book, move or cancel
    void this.realtime.connect();
    this.realtime.scheduleChanges$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => this.onScheduleChanged(event));
  }

  onScheduleChanged({ change, appointment: a }: ScheduleChangedEvent) {
    const when = new Date(a.startTime).toLocaleString(undefined, {
      weekday: 'short', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit'
    });
    const messages: Record<string, string> = {
      booked: `New booking: ${a.patientName}, ${when}`,
      rescheduled: `${a.patientName} moved their visit to ${when}`,
      cancelled: `Cancelled: ${a.patientName}, ${when}`
    };
    if (messages[change]) {
      this.toast.show(messages[change]);
    }

    // Reload if the change touches the day on screen (a move may also have left it)
    const onThisDay = a.startTime.startsWith(this.date());
    const wasOnThisDay = this.appointments().some(x => x.id === a.id);
    if (onThisDay || wasOnThisDay) {
      this.load(true);
    }
  }

  shiftDay(days: number) {
    this.date.set(toIsoDate(addDays(this.dateObj(), days)));
    this.load();
  }

  goToday() {
    this.date.set(toIsoDate(new Date()));
    this.load();
  }

  onDateInput(value: string) {
    if (!value) return;
    this.date.set(value);
    this.load();
  }

  load(quiet = false) {
    if (!quiet) {
      this.loading.set(true);
      this.error.set(null);
      this.activeId.set(null);
    }

    this.appointmentService.getSchedule(this.date()).subscribe({
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

  hasStarted(a: Appointment): boolean {
    return new Date(a.startTime) <= new Date();
  }

  openNotes(a: Appointment) {
    this.activeId.set(a.id);
    this.notes = a.notes ?? '';
  }

  setStatus(a: Appointment, status: FinalStatus) {
    if (status === 'Cancelled' && !confirm(`Cancel ${a.patientName}'s appointment?`)) return;

    this.savingId.set(a.id);
    this.error.set(null);
    const notes = status === 'Completed' ? this.notes : null;

    this.appointmentService.updateStatus(a.id, status, notes).subscribe({
      next: updated => {
        this.appointments.update(list => list.map(x => (x.id === updated.id ? updated : x)));
        this.savingId.set(null);
        this.activeId.set(null);
        this.notes = '';
      },
      error: err => {
        this.error.set(apiError(err));
        this.savingId.set(null);
      }
    });
  }
}
