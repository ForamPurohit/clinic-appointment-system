import { TestBed } from '@angular/core/testing';
import { StarsComponent } from './stars.component';

describe('StarsComponent', () => {
  function render(value: number | null, count: number | null = null) {
    const fixture = TestBed.createComponent(StarsComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('count', count);
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('.stars') as HTMLElement;
  }

  it('rounds to whole stars and shows the value and count', () => {
    const el = render(4.3, 12);
    expect(el.querySelector('.stars__glyphs')?.textContent).toBe('★★★★☆');
    expect(el.textContent).toContain('4.3');
    expect(el.textContent).toContain('(12)');
    expect(el.getAttribute('aria-label')).toBe('Rated 4.3 out of 5, 12 reviews');
  });

  it('uses the singular for one review', () => {
    expect(render(5, 1).getAttribute('aria-label')).toBe('Rated 5.0 out of 5, 1 review');
  });

  it('describes a missing rating for screen readers', () => {
    expect(render(null).getAttribute('aria-label')).toBe('No ratings yet');
  });
});
