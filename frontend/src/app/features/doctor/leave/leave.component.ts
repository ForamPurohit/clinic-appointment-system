import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DoctorLeave } from '../../../core/models/models';
import { DoctorService } from '../../../core/services/doctor.service';
import { apiError, toIsoDate } from '../../../core/utils';

@Component({
  selector: 'app-leave',
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './leave.component.html'
})
export class LeaveComponent implements OnInit {
  private doctors = inject(DoctorService);

  today = toIsoDate(new Date());
  leaves = signal<DoctorLeave[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);
  listError = signal<string | null>(null);

  startDate = '';
  endDate = '';
  reason = '';

  ngOnInit() {
    this.load();
  }

  load() {
    this.doctors.getMyLeaves().subscribe({
      next: list => {
        this.leaves.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.listError.set(apiError(err));
        this.loading.set(false);
      }
    });
  }

  dayCount(l: DoctorLeave): number {
    const ms = new Date(l.endDate + 'T00:00:00').getTime() - new Date(l.startDate + 'T00:00:00').getTime();
    return Math.round(ms / 86_400_000) + 1;
  }

  onStartChange(value: string) {
    this.startDate = value;
    if (!this.endDate || this.endDate < value) {
      this.endDate = value;
    }
  }

  add() {
    if (!this.startDate || !this.endDate) {
      this.error.set('Choose the first and last day of your leave.');
      return;
    }
    if (this.endDate < this.startDate) {
      this.error.set("The last day can't be before the first day.");
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.doctors.addLeave({ startDate: this.startDate, endDate: this.endDate, reason: this.reason.trim() || null }).subscribe({
      next: leave => {
        this.leaves.update(list => [...list, leave].sort((a, b) => a.startDate.localeCompare(b.startDate)));
        this.startDate = this.endDate = this.reason = '';
        this.saving.set(false);
      },
      error: err => {
        this.error.set(apiError(err, "Couldn't add this leave."));
        this.saving.set(false);
      }
    });
  }

  remove(l: DoctorLeave) {
    if (!confirm('Remove this leave? Patients will be able to book these days again.')) return;
    this.doctors.deleteLeave(l.id).subscribe({
      next: () => this.leaves.update(list => list.filter(x => x.id !== l.id)),
      error: err => this.listError.set(apiError(err))
    });
  }
}
