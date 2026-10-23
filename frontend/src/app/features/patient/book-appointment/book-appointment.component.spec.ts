import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { SpecializationService } from '../../../core/services/specialization.service';
import { FakeRealtimeService, MONDAY_8AM, SPECIALIZATIONS, appointment, doctor, slots } from '../../../../testing/test-data';
import { BookAppointmentComponent } from './book-appointment.component';

describe('BookAppointmentComponent (UC-03 Find a doctor, UC-04 Book, UC-17 Reviews)', () => {
  let fixture: ComponentFixture<BookAppointmentComponent>;
  let component: BookAppointmentComponent;
  let el: HTMLElement;
  let doctors: jasmine.SpyObj<DoctorService>;
  let appointments: jasmine.SpyObj<AppointmentService>;

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(MONDAY_8AM);

    const specializations = jasmine.createSpyObj<SpecializationService>('SpecializationService', ['getAll']);
    specializations.getAll.and.returnValue(of(SPECIALIZATIONS));
    doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getAll', 'getSlots', 'getLeaves', 'getReviews']);
    doctors.getAll.and.returnValue(of([doctor(), doctor({ id: 12, fullName: 'Dr. New', reviewCount: 0, averageRating: null })]));
    doctors.getSlots.and.callFake((_id, date) => of(slots(date)));
    doctors.getLeaves.and.returnValue(of([]));
    doctors.getReviews.and.returnValue(of({
      averageRating: 4.5, reviewCount: 2,
      reviews: [{ id: 1, appointmentId: 1, doctorId: 10, patientName: 'Rahul V.', rating: 5, comment: 'Very kind', createdAt: '2026-10-01T10:00:00' }]
    }));
    appointments = jasmine.createSpyObj<AppointmentService>('AppointmentService', ['book']);

    TestBed.configureTestingModule({
      imports: [BookAppointmentComponent],
      providers: [
        provideRouter([]),
        { provide: SpecializationService, useValue: specializations },
        { provide: DoctorService, useValue: doctors },
        { provide: AppointmentService, useValue: appointments },
        { provide: RealtimeService, useClass: FakeRealtimeService }
      ]
    });

    fixture = TestBed.createComponent(BookAppointmentComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => jasmine.clock().uninstall());

  /** Picks General Medicine, the first doctor, and the first open time shown by the slot picker. */
  function pickDoctorAndSlot() {
    component.chooseSpecialization(SPECIALIZATIONS[0]);
    component.chooseDoctor(component.doctors()[0]);
    fixture.detectChanges();
    (el.querySelector('.slot:not([disabled])') as HTMLButtonElement).click();
    fixture.detectChanges();
    return component.selectedSlot()!;
  }

  it('lists specialties and disables ones with no doctors', () => {
    const chips = Array.from(el.querySelectorAll<HTMLButtonElement>('.chip'));
    expect(chips.map(c => c.textContent?.trim())).toEqual(['General Medicine', 'Dermatology']);
    expect(chips[1].disabled).toBeTrue();
  });

  it('loads doctors for the chosen specialty with their ratings', () => {
    component.chooseSpecialization(SPECIALIZATIONS[0]);
    fixture.detectChanges();

    expect(doctors.getAll).toHaveBeenCalledWith(1);
    const cards = el.querySelectorAll('.doctor-option');
    expect(cards[0].querySelector('.stars')?.getAttribute('aria-label')).toBe('Rated 4.5 out of 5, 2 reviews');
    expect(cards[1].textContent).toContain('no reviews yet');
  });

  it('shows reviews on request, without selecting the doctor', () => {
    component.chooseSpecialization(SPECIALIZATIONS[0]);
    fixture.detectChanges();

    (el.querySelector('.doctor-option .link-button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(doctors.getReviews).toHaveBeenCalledWith(10);
    expect(el.querySelector('.review-list')?.textContent).toContain('Very kind');
    expect(component.selectedDoctor()).toBeNull();
  });

  it('shows the slot picker once a doctor is chosen', () => {
    component.chooseSpecialization(SPECIALIZATIONS[0]);
    component.chooseDoctor(component.doctors()[0]);
    fixture.detectChanges();

    expect(el.querySelector('app-slot-picker')).not.toBeNull();
    expect(doctors.getSlots).toHaveBeenCalledWith(10, '2026-10-19');
  });

  it('keeps the book button disabled until a reason is given', () => {
    pickDoctorAndSlot();
    const button = () => Array.from(el.querySelectorAll<HTMLButtonElement>('button'))
      .find(b => b.textContent?.includes('Book appointment'))!;

    expect(button().disabled).toBeTrue();
    component.reason = 'Fever since yesterday';
    fixture.detectChanges();
    expect(button().disabled).toBeFalse();
  });

  it('books the slot and shows the confirmation', () => {
    const slot = pickDoctorAndSlot();
    appointments.book.and.returnValue(of(appointment({ startTime: slot.startTime })));
    component.reason = '  Fever since yesterday  ';

    component.confirm();
    fixture.detectChanges();

    expect(appointments.book).toHaveBeenCalledWith({ doctorId: 10, startTime: slot.startTime, reason: 'Fever since yesterday' });
    expect(el.querySelector('.confirmation h2')?.textContent).toContain("You're booked in");
    expect(el.textContent).toContain('confirmation email');
  });

  it('when the slot was just taken, shows the error and refreshes the times', () => {
    pickDoctorAndSlot();
    appointments.book.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 409, error: { message: 'This time slot was just booked. Pick another slot.' } })));
    doctors.getSlots.calls.reset();
    component.reason = 'Fever';

    component.confirm();
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('just booked');
    expect(component.selectedSlot()).toBeNull();
    expect(doctors.getSlots).toHaveBeenCalledTimes(1);
  });

  it('changing specialty clears the doctor and slot', () => {
    pickDoctorAndSlot();
    component.chooseSpecialization(SPECIALIZATIONS[0]);
    expect(component.selectedDoctor()).toBeNull();
    expect(component.selectedSlot()).toBeNull();
  });

  it('"Book another" resets the whole flow', () => {
    pickDoctorAndSlot();
    appointments.book.and.returnValue(of(appointment()));
    component.reason = 'Fever';
    component.confirm();

    component.startOver();

    expect(component.booked()).toBeNull();
    expect(component.selectedSpecialization()).toBeNull();
    expect(component.reason).toBe('');
  });
});
