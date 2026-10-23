import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { DashboardStats, PatientSummary } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/admin`;

  getStats() {
    return this.http.get<DashboardStats>(`${this.baseUrl}/stats`);
  }

  getPatients(search?: string) {
    const params = search ? new HttpParams().set('search', search) : undefined;
    return this.http.get<PatientSummary[]>(`${this.baseUrl}/patients`, { params });
  }
}
