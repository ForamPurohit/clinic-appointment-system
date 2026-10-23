import { Component, OnInit, inject, signal, viewChild } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Appointment, Doctor, DoctorReviews, Specialization, TimeSlot, WEEKDAYS } from '../../../core/models/models';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { SpecializationService } from '../../../core/services/specialization.service';
import { apiError } from '../../../core/utils';
import { SlotPickerComponent } from '../../../shared/slot-picker/slot-picker.component';
import { StarsComponent } from '../../../shared/stars/stars.component';

@Component({
  selector: 'app-book-appointment',
  standalone: true,
  imports: [FormsModule, DatePipe, CurrencyPipe, RouterLink, SlotPickerComponent, StarsComponent],
  templateUrl: './book-appointment.component.html'
})
export class BookAppointmentComponent implements OnInit {
  private specializationService = inject(SpecializationService);
  private doctorService = inject(DoctorService);
  private appointmentService = inject(AppointmentService);

  picker = viewChild(SlotPickerComponent);

  specializations = signal<Specialization[]>([]);
  doctors = signal<Doctor[]>([]);

  selectedSpecialization = signal<Specialization | null>(null);
  selectedDoctor = signal<Doctor | null>(null);
  selectedSlot = signal<TimeSlot | null>(null);
  reason = '';

  /** Reviews shown under a doctor card, loaded when the patient asks for them. */
  openReviewsFor = signal<number | null>(null);
  reviews = signal<DoctorReviews | null>(null);

  loadingDoctors = signal(false);
  booking = signal(false);
  error = signal<string | null>(null);
  booked = signal<Appointment | null>(null);

  ngOnInit() {
    this.specializationService.getAll().subscribe({
      next: list => this.specializations.set(list),
      error: err => this.error.set(apiError(err))
    });
  }

  workingDaysLabel(doctor: Doctor): string {
    return doctor.workingDays.map(d => WEEKDAYS[d]).join(', ');
  }

  chooseSpecialization(s: Specialization) {
    this.selectedSpecialization.set(s);
    this.selectedDoctor.set(null);
    this.selectedSlot.set(null);
    this.openReviewsFor.set(null);
    this.error.set(null);
    this.loadingDoctors.set(true);

    this.doctorService.getAll(s.id).subscribe({
      next: list => {
        this.doctors.set(list);
        this.loadingDoctors.set(false);
      },
      error: err => {
        this.error.set(apiError(err));
        this.loadingDoctors.set(false);
      }
    });
  }

  chooseDoctor(d: Doctor) {
    if (this.selectedDoctor()?.id === d.id) return;
    this.selectedDoctor.set(d);
    this.selectedSlot.set(null);
    this.error.set(null);
  }

  toggleReviews(d: Doctor, event: Event) {
    event.stopPropagation();
    if (this.openReviewsFor() === d.id) {
      this.openReviewsFor.set(null);
      return;
    }
    this.openReviewsFor.set(d.id);
    this.reviews.set(null);
    this.doctorService.getReviews(d.id).subscribe({
      next: r => this.reviews.set(r),
      error: () => this.reviews.set({ averageRating: null, reviewCount: 0, reviews: [] })
    });
  }

  onSlotChange(slot: TimeSlot | null) {
    this.selectedSlot.set(slot);
    if (slot) this.error.set(null);
  }

  confirm() {
    const doctor = this.selectedDoctor();
    const slot = this.selectedSlot();
    if (!doctor || !slot || this.reason.trim().length < 3) return;

    this.booking.set(true);
    this.error.set(null);

    this.appointmentService.book({ doctorId: doctor.id, startTime: slot.startTime, reason: this.reason.trim() })
      .subscribe({
        next: appointment => {
          this.booked.set(appointment);
          this.booking.set(false);
        },
        error: err => {
          this.error.set(apiError(err, "Couldn't book this slot. Try another time."));
          this.booking.set(false);
          // Someone may have just taken the slot, so show fresh times
          this.picker()?.refresh();
        }
      });
  }

  startOver() {
    this.booked.set(null);
    this.selectedSpecialization.set(null);
    this.selectedDoctor.set(null);
    this.selectedSlot.set(null);
    this.doctors.set([]);
    this.reason = '';
    this.error.set(null);
  }
}
