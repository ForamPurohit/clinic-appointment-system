import { TestBed } from '@angular/core/testing';
import { ToastService } from '../../core/services/toast.service';
import { ToastsComponent } from './toasts.component';

describe('ToastsComponent (UC-18 live notifications)', () => {
  it('shows each toast and removes it when dismissed', () => {
    const fixture = TestBed.createComponent(ToastsComponent);
    const service = TestBed.inject(ToastService);
    service.show('New booking: Asha Rao, Tue 20 Oct, 10:30', 60_000);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.toast')?.textContent).toContain('New booking: Asha Rao');

    (el.querySelector('.toast button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('.toast')).toBeNull();
  });

  it('announces toasts to screen readers', () => {
    const fixture = TestBed.createComponent(ToastsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.toasts')?.getAttribute('aria-live')).toBe('polite');
  });
});
