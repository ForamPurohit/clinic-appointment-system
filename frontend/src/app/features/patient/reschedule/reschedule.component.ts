import { Component, OnInit, computed, inject, input, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Appointment, Doctor, MAX_PATIENT_RESCHEDULES, TimeSlot } from '../../../core/models/models';
import { AppointmentService } from '../../../core/services/appointment.service';
import { AuthService } from '../../../core/services/auth.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { ToastService } from '../../../core/services/toast.service';
import { apiError } from '../../../core/utils';
import { SlotPickerComponent } from '../../../shared/slot-picker/slot-picker.component';

@Component({
  selector: 'app-reschedule',
  standalone: true,
  imports: [DatePipe, RouterLink, SlotPickerComponent],
  templateUrl: './reschedule.component.html'
})
export class RescheduleComponent implements OnInit {
  private appointments = inject(AppointmentService);
  private doctors = inject(DoctorService);
  private auth = inject(AuthService);
  private toast = inject(ToastService);
  private router = inject(Router);

  /** Route parameter, bound by withComponentInputBinding(). */
  id = input.required<string>();

  picker = viewChild(SlotPickerComponent);

  appointment = signal<Appointment | null>(null);
  doctor = signal<Doctor | null>(null);
  newSlot = signal<TimeSlot | null>(null);
  saving = signal(false);
  loadError = signal<string | null>(null);
  error = signal<string | null>(null);

  isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  backLink = computed(() => (this.isAdmin() ? '/admin/appointments' : '/patient/appointments'));
  movesLeft = computed(() => MAX_PATIENT_RESCHEDULES - (this.appointment()?.rescheduleCount ?? 0));
  canMove = computed(() => {
    const a = this.appointment();
    if (!a || a.status !== 'Scheduled' || new Date(a.startTime) <= new Date()) return false;
    return this.isAdmin() || this.movesLeft() > 0;
  });

  ngOnInit() {
    this.appointments.getById(Number(this.id())).subscribe({
      next: a => {
        this.appointment.set(a);
        this.doctors.getById(a.doctorId).subscribe({
          next: d => this.doctor.set(d),
          error: err => this.loadError.set(apiError(err))
        });
      },
      error: err => this.loadError.set(apiError(err, "Couldn't load this appointment."))
    });
  }

  confirm() {
    const a = this.appointment();
    const slot = this.newSlot();
    if (!a || !slot) return;

    this.saving.set(true);
    this.error.set(null);
    this.appointments.reschedule(a.id, slot.startTime).subscribe({
      next: moved => {
        this.toast.show(`Moved to ${new Date(moved.startTime).toLocaleString(undefined, {
          weekday: 'short', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit'
        })}. We've emailed the new details.`);
        this.router.navigateByUrl(this.backLink());
      },
      error: err => {
        this.error.set(apiError(err, "Couldn't move the appointment. Try another time."));
        this.saving.set(false);
        this.picker()?.refresh();
      }
    });
  }
}
