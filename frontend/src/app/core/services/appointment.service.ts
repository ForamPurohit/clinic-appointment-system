import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Appointment, AppointmentFilter, AppointmentStatus, CreateAppointmentRequest, Review } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AppointmentService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/appointments`;

  book(body: CreateAppointmentRequest) {
    return this.http.post<Appointment>(this.baseUrl, body);
  }

  getMine() {
    return this.http.get<Appointment[]>(`${this.baseUrl}/mine`);
  }

  getSchedule(date?: string) {
    const params = date ? new HttpParams().set('date', date) : undefined;
    return this.http.get<Appointment[]>(`${this.baseUrl}/schedule`, { params });
  }

  getAll(filter: AppointmentFilter = {}) {
    let params = new HttpParams();
    if (filter.status) params = params.set('status', filter.status);
    if (filter.doctorId) params = params.set('doctorId', filter.doctorId);
    if (filter.date) params = params.set('date', filter.date);
    return this.http.get<Appointment[]>(this.baseUrl, { params });
  }

  getById(id: number) {
    return this.http.get<Appointment>(`${this.baseUrl}/${id}`);
  }

  reschedule(id: number, newStartTime: string) {
    return this.http.patch<Appointment>(`${this.baseUrl}/${id}/reschedule`, { newStartTime });
  }

  review(id: number, rating: number, comment?: string | null) {
    return this.http.post<Review>(`${this.baseUrl}/${id}/review`, { rating, comment });
  }

  cancel(id: number) {
    return this.http.patch<Appointment>(`${this.baseUrl}/${id}/cancel`, {});
  }

  updateStatus(id: number, status: Exclude<AppointmentStatus, 'Scheduled'>, notes?: string | null) {
    return this.http.patch<Appointment>(`${this.baseUrl}/${id}/status`, { status, notes });
  }
}
