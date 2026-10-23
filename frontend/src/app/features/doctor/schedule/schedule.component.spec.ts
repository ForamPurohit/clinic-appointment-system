import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { ToastService } from '../../../core/services/toast.service';
import { FakeRealtimeService, MONDAY_8AM, appointment, doctor } from '../../../../testing/test-data';
import { ScheduleComponent } from './schedule.component';

describe('ScheduleComponent (UC-07 Daily schedule, UC-08 Record outcome, UC-18 Live updates)', () => {
  let fixture: ComponentFixture<ScheduleComponent>;
  let component: ScheduleComponent;
  let el: HTMLElement;
  let appointments: jasmine.SpyObj<AppointmentService>;
  let realtime: FakeRealtimeService;
  let toast: ToastService;

  const past = appointment({ id: 1, startTime: '2026-10-19T07:30:00', endTime: '2026-10-19T08:00:00', patientName: 'Early Bird' });
  const future = appointment({ id: 2, startTime: '2026-10-19T10:00:00', endTime: '2026-10-19T10:30:00', patientName: 'Later Visit' });

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(MONDAY_8AM);

    appointments = jasmine.createSpyObj<AppointmentService>('AppointmentService', ['getSchedule', 'updateStatus']);
    appointments.getSchedule.and.returnValue(of([past, future]));
    const doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getMe']);
    doctors.getMe.and.returnValue(of(doctor()));

    realtime = new FakeRealtimeService();
    TestBed.configureTestingModule({
      imports: [ScheduleComponent],
      providers: [
        { provide: AppointmentService, useValue: appointments },
        { provide: DoctorService, useValue: doctors },
        { provide: RealtimeService, useValue: realtime }
      ]
    });
    toast = TestBed.inject(ToastService);
    fixture = TestBed.createComponent(ScheduleComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => jasmine.clock().uninstall());

  it("loads today's schedule on open", () => {
    expect(appointments.getSchedule).toHaveBeenCalledWith('2026-10-19');
    expect(component.isToday()).toBeTrue();
    expect(el.textContent).toContain('Early Bird');
    expect(el.textContent).toContain('2 booked, 2 still to see');
  });

  it('shows the clinic hours and rating from the profile', () => {
    expect(el.textContent).toContain('Mon, Tue, Wed, Thu, Fri, 09:00–11:00, 30-minute slots');
    expect(el.textContent).toContain('Patients rate you 4.5 out of 5 from 2 reviews');
  });

  it('connects for live updates', () => {
    expect(realtime.connect).toHaveBeenCalled();
  });

  it('announces a new booking for today and reloads the list', () => {
    const show = spyOn(toast, 'show');
    appointments.getSchedule.calls.reset();

    realtime.emitSchedule({ change: 'booked', appointment: appointment({ id: 9, patientName: 'Asha Rao', startTime: '2026-10-19T10:30:00' }) });

    expect(show.calls.mostRecent().args[0]).toContain('New booking: Asha Rao');
    expect(appointments.getSchedule).toHaveBeenCalledWith('2026-10-19');
  });

  it('reloads when a patient moves a visit away from the day on screen', () => {
    appointments.getSchedule.calls.reset();

    realtime.emitSchedule({ change: 'rescheduled', appointment: { ...future, startTime: '2026-10-23T09:00:00' } });

    expect(appointments.getSchedule).toHaveBeenCalledTimes(1);
  });

  it('announces cancellations', () => {
    const show = spyOn(toast, 'show');

    realtime.emitSchedule({ change: 'cancelled', appointment: { ...future, status: 'Cancelled' } });

    expect(show.calls.mostRecent().args[0]).toContain('Cancelled: Later Visit');
  });

  it('does not reload for changes on other days', () => {
    appointments.getSchedule.calls.reset();

    realtime.emitSchedule({ change: 'booked', appointment: appointment({ id: 99, startTime: '2026-10-23T09:00:00' }) });

    expect(appointments.getSchedule).not.toHaveBeenCalled();
  });

  it('moves between days', () => {
    component.shiftDay(1);
    expect(appointments.getSchedule).toHaveBeenCalledWith('2026-10-20');

    component.shiftDay(-2);
    expect(appointments.getSchedule).toHaveBeenCalledWith('2026-10-18');
    expect(component.isToday()).toBeFalse();

    component.goToday();
    expect(component.date()).toBe('2026-10-19');
  });

  it('only offers Complete and No-show once a visit has started', () => {
    expect(component.hasStarted(past)).toBeTrue();
    expect(component.hasStarted(future)).toBeFalse();

    const completeButtons = Array.from(el.querySelectorAll('button')).filter(b => b.textContent?.includes('Complete visit'));
    expect(completeButtons.length).toBe(1);
  });

  it('completes a visit with notes', () => {
    appointments.updateStatus.and.returnValue(of({ ...past, status: 'Completed', notes: 'Rest' }));

    component.openNotes(past);
    component.notes = 'Rest';
    component.setStatus(past, 'Completed');

    expect(appointments.updateStatus).toHaveBeenCalledWith(1, 'Completed', 'Rest');
    expect(component.appointments().find(a => a.id === 1)?.status).toBe('Completed');
    expect(component.activeId()).toBeNull();
  });

  it('records a no-show without notes', () => {
    appointments.updateStatus.and.returnValue(of({ ...past, status: 'NoShow' }));

    component.setStatus(past, 'NoShow');

    expect(appointments.updateStatus).toHaveBeenCalledWith(1, 'NoShow', null);
  });

  it('asks before cancelling and stops if the doctor declines', () => {
    spyOn(window, 'confirm').and.returnValue(false);

    component.setStatus(future, 'Cancelled');

    expect(appointments.updateStatus).not.toHaveBeenCalled();
  });

  it('shows API errors', () => {
    appointments.updateStatus.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 400, error: { message: 'This appointment is already Completed.' } })));

    component.setStatus(past, 'NoShow');
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('already Completed');
    expect(component.savingId()).toBeNull();
  });

  it('shows an empty state for a free day', () => {
    appointments.getSchedule.and.returnValue(of([]));
    component.shiftDay(1);
    fixture.detectChanges();

    expect(el.textContent).toContain('No appointments on this day.');
  });
});
