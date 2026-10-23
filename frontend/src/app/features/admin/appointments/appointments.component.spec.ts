import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AppointmentService } from '../../../core/services/appointment.service';
import { DoctorService } from '../../../core/services/doctor.service';
import { appointment, doctor } from '../../../../testing/test-data';
import { AdminAppointmentsComponent } from './appointments.component';

describe('AdminAppointmentsComponent (UC-12 Oversee appointments)', () => {
  let service: jasmine.SpyObj<AppointmentService>;
  let component: AdminAppointmentsComponent;

  beforeEach(() => {
    service = jasmine.createSpyObj<AppointmentService>('AppointmentService', ['getAll', 'cancel']);
    service.getAll.and.returnValue(of([appointment({ id: 1 })]));
    const doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getAll']);
    doctors.getAll.and.returnValue(of([doctor()]));

    TestBed.configureTestingModule({
      imports: [AdminAppointmentsComponent],
      providers: [
        provideRouter([]),
        { provide: AppointmentService, useValue: service },
        { provide: DoctorService, useValue: doctors }
      ]
    });
    const fixture = TestBed.createComponent(AdminAppointmentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('passes the chosen filters to the API', () => {
    component.filter = { status: 'Completed', doctorId: 10, date: '2026-10-19' };
    component.load();
    expect(service.getAll).toHaveBeenCalledWith({ status: 'Completed', doctorId: 10, date: '2026-10-19' });
  });

  it('clearing filters reloads everything', () => {
    component.filter = { status: 'Completed', doctorId: 10, date: '2026-10-19' };
    component.clearFilters();
    expect(service.getAll).toHaveBeenCalledWith({ status: '', doctorId: null, date: '' });
  });

  it('offers Move for scheduled appointments', () => {
    const fixture = TestBed.createComponent(AdminAppointmentsComponent);
    fixture.detectChanges();
    const move = (fixture.nativeElement as HTMLElement).querySelector('a.btn');
    expect(move?.getAttribute('href')).toBe('/reschedule/1');
  });

  it('cancels a booking in place', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    service.cancel.and.returnValue(of(appointment({ id: 1, status: 'Cancelled' })));

    component.cancel(component.appointments()[0]);

    expect(component.appointments()[0].status).toBe('Cancelled');
  });
});
