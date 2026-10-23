import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { Role } from '../../core/models/models';
import { AuthService } from '../../core/services/auth.service';
import { storeSession } from '../../../testing/test-data';
import { NavbarComponent } from './navbar.component';

describe('NavbarComponent', () => {
  function render(role?: Role) {
    localStorage.clear();
    if (role) storeSession(role, { user: { id: 1, fullName: 'Dr. Priya Sharma', email: 'x@y.com', role } });
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
    const fixture = TestBed.createComponent(NavbarComponent);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  const linkTexts = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('#main-nav > a')).map(a => a.textContent?.trim());

  afterEach(() => localStorage.clear());

  it('shows sign-in links to visitors', () => {
    const { el } = render();
    expect(el.textContent).toContain('Sign in');
    expect(el.textContent).toContain('Create account');
  });

  it('shows patient links', () => {
    expect(linkTexts(render('Patient').el)).toEqual(['My appointments', 'Book a visit']);
  });

  it('shows doctor links', () => {
    expect(linkTexts(render('Doctor').el)).toEqual(['My schedule', 'Time off']);
  });

  it('shows all admin sections', () => {
    expect(linkTexts(render('Admin').el)).toEqual(['Overview', 'Appointments', 'Doctors', 'Specialties', 'Patients']);
  });

  it('builds initials without the "Dr." title', () => {
    const { fixture } = render('Doctor');
    expect(fixture.componentInstance.initials()).toBe('PS');
  });

  it('signs out from the button', () => {
    const { el } = render('Patient');
    const logout = spyOn(TestBed.inject(AuthService), 'logout');
    (Array.from(el.querySelectorAll('button')).find(b => b.textContent?.includes('Sign out')) as HTMLButtonElement).click();
    expect(logout).toHaveBeenCalled();
  });

  it('toggles the mobile menu', () => {
    const { fixture } = render('Patient');
    fixture.componentInstance.toggleMenu();
    expect(fixture.componentInstance.menuOpen()).toBeTrue();
    fixture.componentInstance.closeMenu();
    expect(fixture.componentInstance.menuOpen()).toBeFalse();
  });
});
