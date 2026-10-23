import { Component, OnChanges, OnDestroy, SimpleChanges, computed, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { Doctor, DoctorLeave, TimeSlot } from '../../core/models/models';
import { DoctorService } from '../../core/services/doctor.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { addDays, apiError, toIsoDate } from '../../core/utils';

export interface DayOption {
  iso: string;
  date: Date;
  works: boolean;
  onLeave: boolean;
}

export const DAYS_AHEAD = 14;

/**
 * Day strip plus time grid for one doctor. Greys out non-working days and leave,
 * shows taken times, and refreshes itself when another patient books or cancels.
 * Used by both booking and rescheduling.
 */
@Component({
  selector: 'app-slot-picker',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './slot-picker.component.html'
})
export class SlotPickerComponent implements OnChanges, OnDestroy {
  private doctorService = inject(DoctorService);
  private realtime = inject(RealtimeService);

  doctor = input.required<Doctor>();
  /** When rescheduling: the appointment's current start, shown as "Current". */
  currentStart = input<string | null>(null);

  slotChange = output<TimeSlot | null>();

  leaves = signal<DoctorLeave[]>([]);
  selectedDate = signal<string | null>(null);
  slots = signal<TimeSlot[]>([]);
  selectedSlot = signal<TimeSlot | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);
  liveUpdated = signal(false);

  private watch?: Subscription;

  days = computed<DayOption[]>(() => {
    const doctor = this.doctor();
    const leaves = this.leaves();
    const today = new Date();
    return Array.from({ length: DAYS_AHEAD }, (_, i) => {
      const date = addDays(today, i);
      const iso = toIsoDate(date);
      return {
        iso,
        date,
        works: doctor.workingDays.includes(date.getDay()),
        onLeave: leaves.some(l => iso >= l.startDate && iso <= l.endDate)
      };
    });
  });

  hasOpenDay = computed(() => this.days().some(d => d.works && !d.onLeave));
  openSlotCount = computed(() => this.slots().filter(s => this.isSelectable(s)).length);
  selectedDay = computed(() => this.days().find(d => d.iso === this.selectedDate()) ?? null);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['doctor']) {
      this.start();
    }
  }

  ngOnDestroy() {
    this.watch?.unsubscribe();
  }

  isCurrent(slot: TimeSlot) {
    return slot.startTime === this.currentStart();
  }

  isSelectable(slot: TimeSlot) {
    return slot.isAvailable && !this.isCurrent(slot);
  }

  chooseDate(day: DayOption) {
    if (!day.works || day.onLeave) return;
    this.selectedDate.set(day.iso);
    this.setSlot(null);
    this.loadSlots();
  }

  chooseSlot(slot: TimeSlot) {
    if (this.isSelectable(slot)) {
      this.setSlot(slot);
    }
  }

  /** Reloads availability, e.g. after a booking attempt lost a race for a slot. */
  refresh() {
    this.setSlot(null);
    this.loadSlots();
  }

  private start() {
    const doctor = this.doctor();
    this.selectedDate.set(null);
    this.slots.set([]);
    this.setSlot(null);
    this.leaves.set([]);
    this.error.set(null);

    this.watch?.unsubscribe();
    this.watch = this.realtime.watchSlots(doctor.id).subscribe(event => {
      this.loadLeaves(false);
      if (event.date === this.selectedDate()) {
        this.loadSlots(true);
      }
    });

    this.loadLeaves(true);
  }

  private loadLeaves(pickFirstDay: boolean) {
    this.doctorService.getLeaves(this.doctor().id).subscribe({
      next: leaves => {
        this.leaves.set(leaves);
        if (pickFirstDay) {
          const first = this.days().find(d => d.works && !d.onLeave);
          if (first) this.chooseDate(first);
        } else if (this.selectedDay()?.onLeave) {
          this.slots.set([]);
          this.setSlot(null);
        }
      },
      error: () => {
        // Without leave data, still let the patient pick a day; the API rejects leave days anyway
        if (pickFirstDay) {
          const first = this.days().find(d => d.works);
          if (first) this.chooseDate(first);
        }
      }
    });
  }

  private loadSlots(live = false) {
    const date = this.selectedDate();
    if (!date) return;

    if (!live) this.loading.set(true);
    this.doctorService.getSlots(this.doctor().id, date).subscribe({
      next: slots => {
        this.slots.set(slots);
        this.loading.set(false);
        this.liveUpdated.set(live);

        // If someone else just took the time this patient picked, clear it
        const picked = this.selectedSlot();
        if (picked && !slots.some(s => s.startTime === picked.startTime && s.isAvailable)) {
          this.setSlot(null);
        }
      },
      error: err => {
        this.error.set(apiError(err));
        this.loading.set(false);
      }
    });
  }

  private setSlot(slot: TimeSlot | null) {
    this.selectedSlot.set(slot);
    this.slotChange.emit(slot);
  }
}
