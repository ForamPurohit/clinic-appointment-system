import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppointmentService } from '../../../core/services/appointment.service';
import { MONDAY_8AM, appointment } from '../../../../testing/test-data';
import { MyAppointmentsComponent } from './my-appointments.component';

describe('MyAppointmentsComponent (UC-05 View, UC-06 Cancel, UC-14 Reschedule, UC-16 Rate)', () => {
  let fixture: ComponentFixture<MyAppointmentsComponent>;
  let component: MyAppointmentsComponent;
  let el: HTMLElement;
  let service: jasmine.SpyObj<AppointmentService>;

  const upcoming = appointment({ id: 1, startTime: '2026-10-20T09:00:00', status: 'Scheduled' });
  const later = appointment({ id: 2, startTime: '2026-10-19T11:00:00', status: 'Scheduled' });
  const completed = appointment({ id: 3, startTime: '2026-10-12T09:00:00', status: 'Completed', notes: 'Drink water' });
  const cancelled = appointment({ id: 4, startTime: '2026-10-21T09:00:00', status: 'Cancelled' });
  const missedScheduled = appointment({ id: 5, startTime: '2026-10-16T09:00:00', status: 'Scheduled' });

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(MONDAY_8AM);

    service = jasmine.createSpyObj<AppointmentService>('AppointmentService', ['getMine', 'cancel', 'review']);
    service.getMine.and.returnValue(of([upcoming, later, completed, cancelled, missedScheduled]));

    TestBed.configureTestingModule({
      imports: [MyAppointmentsComponent],
      providers: [provideRouter([]), { provide: AppointmentService, useValue: service }]
    });
    fixture = TestBed.createComponent(MyAppointmentsComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => jasmine.clock().uninstall());

  it('puts future scheduled visits under Upcoming, soonest first', () => {
    expect(component.upcoming().map(a => a.id)).toEqual([2, 1]);
  });

  it('puts completed, cancelled and past visits under Past', () => {
    expect(component.past().map(a => a.id).sort()).toEqual([3, 4, 5]);
  });

  it('shows tab counts', () => {
    expect(el.textContent).toContain('Upcoming (2)');
    expect(el.textContent).toContain('Past and cancelled (3)');
  });

  it("shows the doctor's notes on past visits", () => {
    component.tab.set('past');
    fixture.detectChanges();
    expect(el.textContent).toContain('Drink water');
  });

  it('offers Reschedule until the patient has moved a visit twice', () => {
    const links = () => Array.from(el.querySelectorAll('a')).filter(a => a.textContent?.trim() === 'Reschedule');
    expect(links().length).toBe(2);
    expect(links()[0].getAttribute('href')).toBe('/reschedule/2');

    service.getMine.and.returnValue(of([{ ...upcoming, rescheduleCount: 2 }]));
    component.load();
    fixture.detectChanges();
    expect(links().length).toBe(0);
  });

  it('offers a rating for recent completed visits that are not yet rated', () => {
    expect(component.canReview(completed)).toBeTrue();
    expect(component.canReview({ ...completed, rating: 4 })).toBeFalse();
    expect(component.canReview(cancelled)).toBeFalse();
    expect(component.canReview({ ...completed, startTime: '2026-08-01T09:00:00' })).toBeFalse();
  });

  it('requires a star rating before submitting', () => {
    component.openReview(completed);
    component.submitReview(completed);
    expect(service.review).not.toHaveBeenCalled();
    expect(component.reviewError()).toContain('1 to 5 stars');
  });

  it('submits a review and shows the rating on the visit', () => {
    service.review.and.returnValue(of({
      id: 1, appointmentId: 3, doctorId: 10, patientName: 'Rahul V.', rating: 4, comment: 'Good', createdAt: ''
    }));
    component.tab.set('past');
    component.openReview(completed);
    component.reviewRating.set(4);
    component.reviewComment = '  Good  ';

    component.submitReview(completed);
    fixture.detectChanges();

    expect(service.review).toHaveBeenCalledWith(3, 4, 'Good');
    expect(component.appointments().find(a => a.id === 3)?.rating).toBe(4);
    expect(el.querySelector('.rated')?.textContent).toContain('You rated');
  });

  it('only offers Cancel on the Upcoming tab', () => {
    const cancelButtons = () =>
      Array.from(el.querySelectorAll('button')).filter(b => b.textContent?.trim() === 'Cancel').length;
    expect(cancelButtons()).toBe(2);

    component.tab.set('past');
    fixture.detectChanges();
    expect(cancelButtons()).toBe(0);
  });

  it('cancels after confirmation and moves the visit to Past', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    service.cancel.and.returnValue(of({ ...upcoming, status: 'Cancelled' }));

    component.cancel(upcoming);

    expect(service.cancel).toHaveBeenCalledWith(1);
    expect(component.upcoming().map(a => a.id)).toEqual([2]);
    expect(component.past().some(a => a.id === 1)).toBeTrue();
  });

  it('does nothing if the patient changes their mind', () => {
    spyOn(window, 'confirm').and.returnValue(false);

    component.cancel(upcoming);

    expect(service.cancel).not.toHaveBeenCalled();
  });

  it('shows an error if cancelling fails', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    service.cancel.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 400, error: { message: "Past appointments can't be cancelled." } })));

    component.cancel(upcoming);
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain("Past appointments can't be cancelled.");
    expect(component.cancellingId()).toBeNull();
  });

  it('shows an invitation to book when there is nothing upcoming', () => {
    service.getMine.and.returnValue(of([completed]));
    component.load();
    fixture.detectChanges();

    expect(el.textContent).toContain('You have no upcoming visits.');
  });
});

describe('MyAppointmentsComponent opened from a review email', () => {
  it('starts on the Past tab when ?tab=past is in the link', async () => {
    const service = jasmine.createSpyObj<AppointmentService>('AppointmentService', ['getMine']);
    service.getMine.and.returnValue(of([]));
    TestBed.configureTestingModule({
      imports: [MyAppointmentsComponent],
      providers: [
        provideRouter([]),
        { provide: AppointmentService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ tab: 'past' }) } } }
      ]
    });
    const fixture = TestBed.createComponent(MyAppointmentsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.tab()).toBe('past');
  });
});
