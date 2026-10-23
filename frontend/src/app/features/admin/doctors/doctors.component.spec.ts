import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { DoctorService } from '../../../core/services/doctor.service';
import { SpecializationService } from '../../../core/services/specialization.service';
import { SPECIALIZATIONS, doctor } from '../../../../testing/test-data';
import { DoctorsComponent } from './doctors.component';

describe('DoctorsComponent (UC-10 Manage doctors)', () => {
  let fixture: ComponentFixture<DoctorsComponent>;
  let component: DoctorsComponent;
  let el: HTMLElement;
  let doctors: jasmine.SpyObj<DoctorService>;

  const active = doctor();
  const inactive = doctor({ id: 11, fullName: 'Dr. Gone', isActive: false });

  beforeEach(() => {
    doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getAll', 'create', 'update']);
    doctors.getAll.and.returnValue(of([active, inactive]));
    const specializations = jasmine.createSpyObj<SpecializationService>('SpecializationService', ['getAll']);
    specializations.getAll.and.returnValue(of(SPECIALIZATIONS));

    TestBed.configureTestingModule({
      imports: [DoctorsComponent],
      providers: [
        { provide: DoctorService, useValue: doctors },
        { provide: SpecializationService, useValue: specializations }
      ]
    });
    fixture = TestBed.createComponent(DoctorsComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  function fillValidForm() {
    component.form.patchValue({
      fullName: 'Dr. New',
      email: 'new@clinic.com',
      password: 'Doctor@123',
      specializationId: 1,
      workStartTime: '10:00',
      workEndTime: '14:00',
      slotDurationMinutes: 20
    });
  }

  it('lists active and inactive doctors', () => {
    expect(doctors.getAll).toHaveBeenCalledWith(null, true);
    const rows = el.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[1].classList).toContain('is-dim');
  });

  it('opens an empty form with Monday to Friday selected', () => {
    component.openCreate();
    expect(component.formOpen()).toBeTrue();
    expect(component.selectedDays()).toEqual([1, 2, 3, 4, 5]);
    expect(component.form.controls.email.enabled).toBeTrue();
  });

  it('toggles working days and keeps them sorted', () => {
    component.openCreate();
    component.toggleDay(3); // remove Wednesday
    component.toggleDay(0); // add Sunday
    component.toggleDay(6); // add Saturday
    expect(component.selectedDays()).toEqual([0, 1, 2, 4, 5, 6]);
  });

  it('refuses to save with no working days', () => {
    component.openCreate();
    fillValidForm();
    [1, 2, 3, 4, 5].forEach(d => component.toggleDay(d));

    component.save();

    expect(doctors.create).not.toHaveBeenCalled();
    expect(component.formError()).toBe('Pick at least one working day.');
  });

  it('refuses to save without a specialty', () => {
    component.openCreate();
    fillValidForm();
    component.form.patchValue({ specializationId: 0 });

    component.save();

    expect(doctors.create).not.toHaveBeenCalled();
  });

  it('creates a doctor with the full payload and reloads the list', () => {
    doctors.create.and.returnValue(of(doctor({ id: 12 })));
    component.openCreate();
    fillValidForm();

    component.save();

    expect(doctors.create).toHaveBeenCalledWith(jasmine.objectContaining({
      fullName: 'Dr. New',
      email: 'new@clinic.com',
      password: 'Doctor@123',
      specializationId: 1,
      workStartTime: '10:00',
      workEndTime: '14:00',
      slotDurationMinutes: 20,
      workingDays: [1, 2, 3, 4, 5],
      phoneNumber: null
    }));
    expect(component.formOpen()).toBeFalse();
    expect(doctors.getAll).toHaveBeenCalledTimes(2);
  });

  it('edits a doctor without sending login details, and can deactivate', () => {
    doctors.update.and.returnValue(of(active));
    component.openEdit(active);
    expect(component.form.controls.email.disabled).toBeTrue();

    component.form.patchValue({ isActive: false });
    component.save();

    const [id, payload] = doctors.update.calls.mostRecent().args;
    expect(id).toBe(10);
    expect(payload.isActive).toBeFalse();
    expect((payload as unknown as Record<string, unknown>)['password']).toBeUndefined();
  });

  it('shows the API error and keeps the form open', () => {
    doctors.create.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 409, error: { message: 'An account with this email already exists.' } })));
    component.openCreate();
    fillValidForm();

    component.save();
    fixture.detectChanges();

    expect(component.formOpen()).toBeTrue();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('already exists');
  });
});
