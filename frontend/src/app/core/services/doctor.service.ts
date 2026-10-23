import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { CreateDoctorRequest, Doctor, DoctorLeave, DoctorReviews, TimeSlot, UpdateDoctorRequest } from '../models/models';

@Injectable({ providedIn: 'root' })
export class DoctorService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/doctors`;

  getAll(specializationId?: number | null, includeInactive = false) {
    let params = new HttpParams();
    if (specializationId) params = params.set('specializationId', specializationId);
    if (includeInactive) params = params.set('includeInactive', true);
    return this.http.get<Doctor[]>(this.baseUrl, { params });
  }

  getById(id: number) {
    return this.http.get<Doctor>(`${this.baseUrl}/${id}`);
  }

  getMe() {
    return this.http.get<Doctor>(`${this.baseUrl}/me`);
  }

  getSlots(doctorId: number, date: string) {
    return this.http.get<TimeSlot[]>(`${this.baseUrl}/${doctorId}/slots`, { params: { date } });
  }

  getLeaves(doctorId: number) {
    return this.http.get<DoctorLeave[]>(`${this.baseUrl}/${doctorId}/leaves`);
  }

  getReviews(doctorId: number, take = 5) {
    return this.http.get<DoctorReviews>(`${this.baseUrl}/${doctorId}/reviews`, { params: { take } });
  }

  getMyLeaves() {
    return this.http.get<DoctorLeave[]>(`${this.baseUrl}/me/leaves`);
  }

  addLeave(body: { startDate: string; endDate: string; reason?: string | null }) {
    return this.http.post<DoctorLeave>(`${this.baseUrl}/me/leaves`, body);
  }

  deleteLeave(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/me/leaves/${id}`);
  }

  create(body: CreateDoctorRequest) {
    return this.http.post<Doctor>(this.baseUrl, body);
  }

  update(id: number, body: UpdateDoctorRequest) {
    return this.http.put<Doctor>(`${this.baseUrl}/${id}`, body);
  }
}
