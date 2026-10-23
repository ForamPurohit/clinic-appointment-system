import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { Appointment } from '../../../core/models/models';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { ToastService } from '../../../core/services/toast.service';
import { FakeRealtimeService, MONDAY_8AM, appointment, doctor, slots, storeSession } from '../../../../testing/test-data';
import { RescheduleComponent } from './reschedule.component';

describe('RescheduleComponent (UC-14 Reschedule)', () => {
  let fixture: ComponentFixture<RescheduleComponent>;
  let component: RescheduleComponent;
  let el: HTMLElement;
  let appointments: jasmine.SpyObj<AppointmentService>;
  let router: Router;
  let toast: ToastService;

  function setup(appt: Partial<Appointment> = {}, role: 'Patient' | 'Admin' = 'Patient') {
    localStorage.clear();
    storeSession(role);
    appointments = jasmine.createSpyObj<AppointmentService>('AppointmentService', ['getById', 'reschedule']);
    appointments.getById.and.returnValue(of(appointment({ id: 5, startTime: '2026-10-19T09:30:00', ...appt })));
    const doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getById', 'getSlots', 'getLeaves']);
    doctors.getById.and.returnValue(of(doctor()));
    doctors.getSlots.and.callFake((_id, date) => of(slots(date)));
    doctors.getLeaves.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [RescheduleComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AppointmentService, useValue: appointments },
        { provide: DoctorService, useValue: doctors },
        { provide: RealtimeService, useClass: FakeRealtimeService }
      ]
    });
    router = TestBed.inject(Router);
    toast = TestBed.inject(ToastService);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    fixture = TestBed.createComponent(RescheduleComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();
  }

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(MONDAY_8AM);
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    localStorage.clear();
  });

  it('loads the appointment and shows its current time in the picker', () => {
    setup();
    expect(appointments.getById).toHaveBeenCalledWith(5);
    expect(el.querySelector('.slot--current')).not.toBeNull();
    expect(el.textContent).toContain('You can move an appointment up to two times.');
  });

  it('keeps Move disabled until a new time is picked', () => {
    setup();
    const move = () => Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent?.includes('Move appointment'))!;
    expect(move().disabled).toBeTrue();

    (el.querySelector('.slot:not([disabled])') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(move().disabled).toBeFalse();
  });

  it('moves the appointment, confirms with a toast and returns to the list', () => {
    setup();
    const show = spyOn(toast, 'show');
    (el.querySelector('.slot:not([disabled])') as HTMLButtonElement).click();
    const picked = component.newSlot()!;
    appointments.reschedule.and.returnValue(of(appointment({ id: 5, startTime: picked.startTime, rescheduleCount: 1 })));

    component.confirm();

    expect(appointments.reschedule).toHaveBeenCalledWith(5, picked.startTime);
    expect(show).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/patient/appointments');
  });

  it('shows the API error and refreshes times when the move fails', () => {
    setup();
    (el.querySelector('.slot:not([disabled])') as HTMLButtonElement).click();
    appointments.reschedule.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 409, error: { message: 'This time slot was just booked. Pick another slot.' } })));

    component.confirm();
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('just booked');
    expect(component.newSlot()).toBeNull();
    expect(component.saving()).toBeFalse();
  });

  it('explains when the patient has used up their moves', () => {
    setup({ rescheduleCount: 2 });
    expect(component.canMove()).toBeFalse();
    expect(el.textContent).toContain('already been moved twice');
    expect(el.querySelector('app-slot-picker')).toBeNull();
  });

  it('lets an admin move it regardless, and returns to the admin list', () => {
    setup({ rescheduleCount: 2 }, 'Admin');
    expect(component.canMove()).toBeTrue();
    expect(component.backLink()).toBe('/admin/appointments');
  });

  it('refuses appointments that are no longer scheduled', () => {
    setup({ status: 'Completed' });
    expect(el.textContent).toContain("is completed and can't be moved");
  });
});
