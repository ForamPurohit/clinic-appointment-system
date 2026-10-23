import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Specialization } from '../models/models';

@Injectable({ providedIn: 'root' })
export class SpecializationService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/specializations`;

  getAll() {
    return this.http.get<Specialization[]>(this.baseUrl);
  }

  create(body: { name: string; description?: string | null }) {
    return this.http.post<Specialization>(this.baseUrl, body);
  }

  update(id: number, body: { name: string; description?: string | null }) {
    return this.http.put<Specialization>(`${this.baseUrl}/${id}`, body);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
