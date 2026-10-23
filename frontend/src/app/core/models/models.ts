export type Role = 'Admin' | 'Doctor' | 'Patient';

export type AppointmentStatus = 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: Role;
  phoneNumber?: string | null;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
}

export interface Specialization {
  id: number;
  name: string;
  description?: string | null;
  doctorCount: number;
}

export interface Doctor {
  id: number;
  userId: number;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  specializationId: number;
  specializationName: string;
  bio?: string | null;
  yearsOfExperience: number;
  consultationFee: number;
  workStartTime: string;
  workEndTime: string;
  slotDurationMinutes: number;
  workingDays: number[];
  isActive: boolean;
  averageRating?: number | null;
  reviewCount: number;
}

export interface DoctorProfileRequest {
  fullName: string;
  phoneNumber?: string | null;
  specializationId: number;
  bio?: string | null;
  yearsOfExperience: number;
  consultationFee: number;
  workStartTime: string;
  workEndTime: string;
  slotDurationMinutes: number;
  workingDays: number[];
}

export interface CreateDoctorRequest extends DoctorProfileRequest {
  email: string;
  password: string;
}

export interface UpdateDoctorRequest extends DoctorProfileRequest {
  isActive: boolean;
}

export interface TimeSlot {
  startTime: string;
  endTime: string;
  isAvailable: boolean;
}

export interface Appointment {
  id: number;
  doctorId: number;
  doctorName: string;
  specializationName: string;
  patientId: number;
  patientName: string;
  patientPhone?: string | null;
  startTime: string;
  endTime: string;
  reason: string;
  notes?: string | null;
  status: AppointmentStatus;
  createdAt: string;
  rescheduleCount: number;
  rating?: number | null;
}

export interface CreateAppointmentRequest {
  doctorId: number;
  startTime: string;
  reason: string;
}

export interface AppointmentFilter {
  status?: string;
  doctorId?: number | null;
  date?: string;
}

export interface DashboardStats {
  totalDoctors: number;
  totalPatients: number;
  totalAppointments: number;
  todayAppointments: number;
  upcomingAppointments: number;
  completedAppointments: number;
  cancelledAppointments: number;
}

export interface PatientSummary {
  id: number;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  appointmentCount: number;
}

export interface DoctorLeave {
  id: number;
  doctorId: number;
  startDate: string;
  endDate: string;
  reason?: string | null;
}

export interface Review {
  id: number;
  appointmentId: number;
  doctorId: number;
  patientName: string;
  rating: number;
  comment?: string | null;
  createdAt: string;
}

export interface DoctorReviews {
  averageRating?: number | null;
  reviewCount: number;
  reviews: Review[];
}

export interface SlotsChangedEvent {
  doctorId: number;
  date: string;
}

export type ScheduleChange = 'booked' | 'rescheduled' | 'cancelled' | 'updated';

export interface ScheduleChangedEvent {
  change: ScheduleChange;
  appointment: Appointment;
}

/** Patients may move an appointment this many times (matches the API). */
export const MAX_PATIENT_RESCHEDULES = 2;

export const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
