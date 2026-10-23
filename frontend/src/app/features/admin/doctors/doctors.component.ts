import { Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Doctor, Specialization, WEEKDAYS } from '../../../core/models/models';
import { DoctorService } from '../../../core/services/doctor.service';
import { SpecializationService } from '../../../core/services/specialization.service';
import { apiError } from '../../../core/utils';
import { StarsComponent } from '../../../shared/stars/stars.component';

@Component({
  selector: 'app-doctors',
  standalone: true,
  imports: [ReactiveFormsModule, CurrencyPipe, StarsComponent],
  templateUrl: './doctors.component.html'
})
export class DoctorsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private doctorService = inject(DoctorService);
  private specializationService = inject(SpecializationService);

  weekdays = WEEKDAYS;
  doctors = signal<Doctor[]>([]);
  specializations = signal<Specialization[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  formError = signal<string | null>(null);

  formOpen = signal(false);
  editing = signal<Doctor | null>(null);
  selectedDays = signal<number[]>([1, 2, 3, 4, 5]);

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    phoneNumber: [''],
    specializationId: [0, Validators.min(1)],
    bio: [''],
    yearsOfExperience: [0, [Validators.min(0), Validators.max(60)]],
    consultationFee: [500, [Validators.min(0)]],
    workStartTime: ['09:00', Validators.required],
    workEndTime: ['17:00', Validators.required],
    slotDurationMinutes: [30, [Validators.min(10), Validators.max(120)]],
    isActive: [true]
  });

  ngOnInit() {
    this.specializationService.getAll().subscribe(list => this.specializations.set(list));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.doctorService.getAll(null, true).subscribe({
      next: list => {
        this.doctors.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(apiError(err));
        this.loading.set(false);
      }
    });
  }

  daysLabel(d: Doctor) {
    return d.workingDays.map(x => WEEKDAYS[x]).join(', ');
  }

  openCreate() {
    this.editing.set(null);
    this.form.reset();
    this.form.controls.email.enable();
    this.form.controls.password.enable();
    this.selectedDays.set([1, 2, 3, 4, 5]);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  openEdit(d: Doctor) {
    this.editing.set(d);
    this.form.reset({
      fullName: d.fullName,
      email: d.email,
      password: '',
      phoneNumber: d.phoneNumber ?? '',
      specializationId: d.specializationId,
      bio: d.bio ?? '',
      yearsOfExperience: d.yearsOfExperience,
      consultationFee: d.consultationFee,
      workStartTime: d.workStartTime,
      workEndTime: d.workEndTime,
      slotDurationMinutes: d.slotDurationMinutes,
      isActive: d.isActive
    });
    // Login details can't be changed from the edit form
    this.form.controls.email.disable();
    this.form.controls.password.disable();
    this.selectedDays.set([...d.workingDays]);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  closeForm() {
    this.formOpen.set(false);
  }

  toggleDay(day: number) {
    this.selectedDays.update(days =>
      days.includes(day) ? days.filter(d => d !== day) : [...days, day].sort((a, b) => a - b)
    );
  }

  save() {
    if (this.form.invalid || this.selectedDays().length === 0) {
      this.form.markAllAsTouched();
      if (this.selectedDays().length === 0) this.formError.set('Pick at least one working day.');
      return;
    }

    const v = this.form.getRawValue();
    const profile = {
      fullName: v.fullName,
      phoneNumber: v.phoneNumber || null,
      specializationId: Number(v.specializationId),
      bio: v.bio || null,
      yearsOfExperience: Number(v.yearsOfExperience),
      consultationFee: Number(v.consultationFee),
      workStartTime: v.workStartTime,
      workEndTime: v.workEndTime,
      slotDurationMinutes: Number(v.slotDurationMinutes),
      workingDays: this.selectedDays()
    };

    this.saving.set(true);
    this.formError.set(null);

    const editing = this.editing();
    const request = editing
      ? this.doctorService.update(editing.id, { ...profile, isActive: v.isActive })
      : this.doctorService.create({ ...profile, email: v.email, password: v.password });

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.load();
      },
      error: err => {
        this.formError.set(apiError(err, "Couldn't save the doctor."));
        this.saving.set(false);
      }
    });
  }
}
