import { Observable, Subject, filter } from 'rxjs';
import { Appointment, AuthResponse, Doctor, Role, ScheduleChangedEvent, SlotsChangedEvent, Specialization, TimeSlot } from '../app/core/models/models';

/** Monday 19 Oct 2026, 08:00 local time. Specs fix "now" here with jasmine.clock().mockDate(). */
export const MONDAY_8AM = new Date(2026, 9, 19, 8, 0, 0);

export function authResponse(role: Role = 'Patient', overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    token: `token-for-${role.toLowerCase()}`,
    expiresAt: '2099-01-01T00:00:00Z',
    user: { id: 7, fullName: `Test ${role}`, email: `${role.toLowerCase()}@clinic.com`, role },
    ...overrides
  };
}

/** Puts a signed-in session in localStorage, the way AuthService stores it. */
export function storeSession(role: Role = 'Patient', overrides: Partial<AuthResponse> = {}): AuthResponse {
  const session = authResponse(role, overrides);
  localStorage.setItem('clinic_auth', JSON.stringify(session));
  return session;
}

export const SPECIALIZATIONS: Specialization[] = [
  { id: 1, name: 'General Medicine', description: 'Check-ups', doctorCount: 1 },
  { id: 2, name: 'Dermatology', description: 'Skin', doctorCount: 0 }
];

export function doctor(overrides: Partial<Doctor> = {}): Doctor {
  return {
    id: 10,
    userId: 20,
    fullName: 'Dr. Priya Sharma',
    email: 'doctor@clinic.com',
    phoneNumber: '9876500001',
    specializationId: 1,
    specializationName: 'General Medicine',
    bio: 'Family physician',
    yearsOfExperience: 8,
    consultationFee: 500,
    workStartTime: '09:00',
    workEndTime: '11:00',
    slotDurationMinutes: 30,
    workingDays: [1, 2, 3, 4, 5],
    isActive: true,
    averageRating: 4.5,
    reviewCount: 2,
    ...overrides
  };
}

export function slots(date = '2026-10-19', taken: string[] = ['09:00']): TimeSlot[] {
  return ['09:00', '09:30', '10:00', '10:30'].map(time => ({
    startTime: `${date}T${time}:00`,
    endTime: `${date}T${time}:00`,
    isAvailable: !taken.includes(time)
  }));
}

let nextId = 100;
export function appointment(overrides: Partial<Appointment> = {}): Appointment {
  return {
    id: nextId++,
    doctorId: 10,
    doctorName: 'Dr. Priya Sharma',
    specializationName: 'General Medicine',
    patientId: 30,
    patientName: 'Rahul Verma',
    patientPhone: '9876500100',
    startTime: '2026-10-19T09:30:00',
    endTime: '2026-10-19T10:00:00',
    reason: 'Fever',
    notes: null,
    status: 'Scheduled',
    createdAt: '2026-10-18T10:00:00',
    rescheduleCount: 0,
    rating: null,
    ...overrides
  };
}

/** Stand-in for RealtimeService: specs push events with emitSlots / emitSchedule. */
export class FakeRealtimeService {
  private slots = new Subject<SlotsChangedEvent>();
  private schedule = new Subject<ScheduleChangedEvent>();
  readonly scheduleChanges$ = this.schedule.asObservable();
  watched: number[] = [];

  connect = jasmine.createSpy('connect').and.resolveTo();

  watchSlots(doctorId: number): Observable<SlotsChangedEvent> {
    this.watched.push(doctorId);
    return this.slots.pipe(filter(e => e.doctorId === doctorId));
  }

  emitSlots(event: SlotsChangedEvent) {
    this.slots.next(event);
  }

  emitSchedule(event: ScheduleChangedEvent) {
    this.schedule.next(event);
  }
}
