import { Component, computed, input } from '@angular/core';
import { AppointmentStatus } from '../../core/models/models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="status" [class]="'status status--' + status().toLowerCase()">{{ label() }}</span>`
})
export class StatusBadgeComponent {
  status = input.required<AppointmentStatus>();
  label = computed(() => (this.status() === 'NoShow' ? 'No-show' : this.status()));
}
