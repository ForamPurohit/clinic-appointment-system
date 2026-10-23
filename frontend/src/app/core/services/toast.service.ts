import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  text: string;
}

/** Short, non-blocking messages, e.g. "New booking: Rahul Verma at 10:00". */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  show(text: string, durationMs = 6000) {
    const toast = { id: this.nextId++, text };
    this.toasts.update(list => [...list.slice(-2), toast]);
    setTimeout(() => this.dismiss(toast.id), durationMs);
  }

  dismiss(id: number) {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
