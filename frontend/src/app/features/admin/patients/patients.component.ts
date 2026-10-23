import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PatientSummary } from '../../../core/models/models';
import { AdminService } from '../../../core/services/admin.service';
import { apiError } from '../../../core/utils';

@Component({
  selector: 'app-patients',
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './patients.component.html'
})
export class PatientsComponent implements OnInit {
  private adminService = inject(AdminService);

  patients = signal<PatientSummary[]>([]);
  error = signal<string | null>(null);
  search = '';

  ngOnInit() {
    this.load();
  }

  load() {
    this.adminService.getPatients(this.search.trim()).subscribe({
      next: list => this.patients.set(list),
      error: err => this.error.set(apiError(err))
    });
  }
}
