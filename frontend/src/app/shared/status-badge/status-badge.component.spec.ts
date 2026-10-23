import { TestBed } from '@angular/core/testing';
import { AppointmentStatus } from '../../core/models/models';
import { StatusBadgeComponent } from './status-badge.component';

describe('StatusBadgeComponent', () => {
  function render(status: AppointmentStatus): HTMLElement {
    const fixture = TestBed.createComponent(StatusBadgeComponent);
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('span');
  }

  it('shows the status with a matching colour class', () => {
    const badge = render('Completed');
    expect(badge.textContent?.trim()).toBe('Completed');
    expect(badge.classList).toContain('status--completed');
  });

  it('writes NoShow as "No-show"', () => {
    const badge = render('NoShow');
    expect(badge.textContent?.trim()).toBe('No-show');
    expect(badge.classList).toContain('status--noshow');
  });
});
