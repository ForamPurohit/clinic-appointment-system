import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Specialization } from '../../../core/models/models';
import { SpecializationService } from '../../../core/services/specialization.service';
import { apiError } from '../../../core/utils';

@Component({
  selector: 'app-specializations',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './specializations.component.html'
})
export class SpecializationsComponent implements OnInit {
  private service = inject(SpecializationService);

  items = signal<Specialization[]>([]);
  error = signal<string | null>(null);
  saving = signal(false);

  // Shared form for add and edit
  editingId = signal<number | null>(null);
  name = '';
  description = '';

  ngOnInit() {
    this.load();
  }

  load() {
    this.service.getAll().subscribe({
      next: list => this.items.set(list),
      error: err => this.error.set(apiError(err))
    });
  }

  edit(s: Specialization) {
    this.editingId.set(s.id);
    this.name = s.name;
    this.description = s.description ?? '';
    this.error.set(null);
  }

  resetForm() {
    this.editingId.set(null);
    this.name = '';
    this.description = '';
  }

  save() {
    if (this.name.trim().length < 2) {
      this.error.set('Enter a specialty name of at least 2 characters.');
      return;
    }

    const body = { name: this.name.trim(), description: this.description.trim() || null };
    const id = this.editingId();
    const request = id ? this.service.update(id, body) : this.service.create(body);

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.resetForm();
        this.load();
      },
      error: err => {
        this.error.set(apiError(err));
        this.saving.set(false);
      }
    });
  }

  remove(s: Specialization) {
    if (!confirm(`Delete ${s.name}?`)) return;
    this.service.delete(s.id).subscribe({
      next: () => this.load(),
      error: err => this.error.set(apiError(err))
    });
  }
}
