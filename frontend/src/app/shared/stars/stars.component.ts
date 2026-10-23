import { Component, computed, input } from '@angular/core';

/** Read-only star rating, e.g. ★★★★☆ 4.3 (12) */
@Component({
  selector: 'app-stars',
  standalone: true,
  template: `
    <span class="stars" [attr.aria-label]="label()">
      <span class="stars__glyphs" aria-hidden="true">{{ glyphs() }}</span>
      @if (showValue() && value() !== null) {
        <span class="stars__value">{{ value()!.toFixed(1) }}</span>
      }
      @if (count() !== null) {
        <span class="stars__count">({{ count() }})</span>
      }
    </span>
  `
})
export class StarsComponent {
  value = input<number | null>(null);
  count = input<number | null>(null);
  showValue = input(true);

  glyphs = computed(() => {
    const filled = Math.round(this.value() ?? 0);
    return '★'.repeat(filled) + '☆'.repeat(5 - filled);
  });

  label = computed(() => {
    const v = this.value();
    if (v === null) return 'No ratings yet';
    const reviews = this.count() === null ? '' : `, ${this.count()} ${this.count() === 1 ? 'review' : 'reviews'}`;
    return `Rated ${v.toFixed(1)} out of 5${reviews}`;
  });
}
