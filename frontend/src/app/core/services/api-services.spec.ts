import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { AdminService } from './admin.service';
import { AppointmentService } from './appointment.service';
import { DoctorService } from './doctor.service';
import { SpecializationService } from './specialization.service';

const api = environment.apiUrl;

describe('API services', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('AppointmentService', () => {
    let service: AppointmentService;
    beforeEach(() => (service = TestBed.inject(AppointmentService)));

    it('book posts the booking', () => {
      service.book({ doctorId: 1, startTime: '2026-10-19T09:00:00', reason: 'Fever' }).subscribe();
      const req = http.expectOne(`${api}/appointments`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.doctorId).toBe(1);
      req.flush({});
    });

    it('getMine calls /mine', () => {
      service.getMine().subscribe();
      expect(http.expectOne(`${api}/appointments/mine`).request.method).toBe('GET');
    });

    it('getSchedule sends the date only when given', () => {
      service.getSchedule('2026-10-19').subscribe();
      expect(http.expectOne(`${api}/appointments/schedule?date=2026-10-19`).request.params.get('date')).toBe('2026-10-19');

      service.getSchedule().subscribe();
      expect(http.expectOne(`${api}/appointments/schedule`).request.params.keys()).toEqual([]);
    });

    it('getAll sends only the filters that are set', () => {
      service.getAll({ status: 'Cancelled', doctorId: null, date: '' }).subscribe();
      const req = http.expectOne(r => r.url === `${api}/appointments`);
      expect(req.request.params.get('status')).toBe('Cancelled');
      expect(req.request.params.has('doctorId')).toBeFalse();
      expect(req.request.params.has('date')).toBeFalse();
      req.flush([]);
    });

    it('reschedule and review send the right bodies', () => {
      service.reschedule(5, '2026-10-20T09:00:00').subscribe();
      const move = http.expectOne(`${api}/appointments/5/reschedule`);
      expect(move.request.method).toBe('PATCH');
      expect(move.request.body).toEqual({ newStartTime: '2026-10-20T09:00:00' });

      service.review(5, 4, 'Great').subscribe();
      const review = http.expectOne(`${api}/appointments/5/review`);
      expect(review.request.method).toBe('POST');
      expect(review.request.body).toEqual({ rating: 4, comment: 'Great' });

      service.getById(5).subscribe();
      expect(http.expectOne(`${api}/appointments/5`).request.method).toBe('GET');
    });

    it('cancel and updateStatus use PATCH', () => {
      service.cancel(5).subscribe();
      expect(http.expectOne(`${api}/appointments/5/cancel`).request.method).toBe('PATCH');

      service.updateStatus(5, 'Completed', 'Rest').subscribe();
      const req = http.expectOne(`${api}/appointments/5/status`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ status: 'Completed', notes: 'Rest' });
    });
  });

  describe('DoctorService', () => {
    let service: DoctorService;
    beforeEach(() => (service = TestBed.inject(DoctorService)));

    it('getAll adds specialization and includeInactive only when set', () => {
      service.getAll().subscribe();
      const plain = http.expectOne(r => r.url === `${api}/doctors`);
      expect(plain.request.params.keys().length).toBe(0);
      plain.flush([]);

      service.getAll(3, true).subscribe();
      const filtered = http.expectOne(r => r.url === `${api}/doctors`);
      expect(filtered.request.params.get('specializationId')).toBe('3');
      expect(filtered.request.params.get('includeInactive')).toBe('true');
      filtered.flush([]);
    });

    it('getSlots asks for one date', () => {
      service.getSlots(4, '2026-10-20').subscribe();
      expect(http.expectOne(`${api}/doctors/4/slots?date=2026-10-20`).request.params.get('date')).toBe('2026-10-20');
    });

    it('reads leave and reviews, and manages the doctor\'s own leave', () => {
      service.getLeaves(4).subscribe();
      expect(http.expectOne(`${api}/doctors/4/leaves`).request.method).toBe('GET');

      service.getReviews(4).subscribe();
      expect(http.expectOne(`${api}/doctors/4/reviews?take=5`).request.params.get('take')).toBe('5');

      service.getMyLeaves().subscribe();
      expect(http.expectOne(`${api}/doctors/me/leaves`).request.method).toBe('GET');

      service.addLeave({ startDate: '2026-10-21', endDate: '2026-10-22' }).subscribe();
      expect(http.expectOne(`${api}/doctors/me/leaves`).request.method).toBe('POST');

      service.deleteLeave(3).subscribe();
      expect(http.expectOne(`${api}/doctors/me/leaves/3`).request.method).toBe('DELETE');
    });

    it('create posts and update puts', () => {
      service.create({} as never).subscribe();
      expect(http.expectOne(`${api}/doctors`).request.method).toBe('POST');

      service.update(9, {} as never).subscribe();
      expect(http.expectOne(`${api}/doctors/9`).request.method).toBe('PUT');
    });
  });

  describe('SpecializationService', () => {
    it('supports list, create, update and delete', () => {
      const service = TestBed.inject(SpecializationService);

      service.getAll().subscribe();
      expect(http.expectOne(`${api}/specializations`).request.method).toBe('GET');

      service.create({ name: 'Neurology' }).subscribe();
      expect(http.expectOne(`${api}/specializations`).request.method).toBe('POST');

      service.update(2, { name: 'Skin' }).subscribe();
      expect(http.expectOne(`${api}/specializations/2`).request.method).toBe('PUT');

      service.delete(2).subscribe();
      expect(http.expectOne(`${api}/specializations/2`).request.method).toBe('DELETE');
    });
  });

  describe('AdminService', () => {
    it('getPatients passes the search term', () => {
      const service = TestBed.inject(AdminService);

      service.getStats().subscribe();
      expect(http.expectOne(`${api}/admin/stats`).request.method).toBe('GET');

      service.getPatients('rahul').subscribe();
      expect(http.expectOne(`${api}/admin/patients?search=rahul`).request.params.get('search')).toBe('rahul');
    });
  });
});
