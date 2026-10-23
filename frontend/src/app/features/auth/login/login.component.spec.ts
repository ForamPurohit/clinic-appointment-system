import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { authResponse } from '../../../../testing/test-data';
import { LoginComponent } from './login.component';

describe('LoginComponent (UC-02 Sign in)', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let router: Router;

  function setup(returnUrl: string | null = null) {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'homeRoute']);
    auth.homeRoute.and.callFake(role => (role === 'Doctor' ? '/doctor/schedule' : '/patient/appointments'));

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(returnUrl ? { returnUrl } : {}) } }
        }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }

  it('does not call the API when the form is empty, and shows field errors', () => {
    const { fixture, component, el } = setup();

    component.submit();
    fixture.detectChanges();

    expect(auth.login).not.toHaveBeenCalled();
    expect(el.textContent).toContain('Enter a valid email address.');
    expect(el.textContent).toContain('Enter your password.');
  });

  it('fills in a demo account', () => {
    const { component } = setup();

    component.useDemo(component.demoAccounts[1]);

    expect(component.form.getRawValue()).toEqual({ email: 'doctor@clinic.com', password: 'Doctor@123' });
  });

  it('signs in and goes to the role home page', () => {
    const { component } = setup();
    auth.login.and.returnValue(of(authResponse('Doctor')));

    component.form.setValue({ email: 'doctor@clinic.com', password: 'Doctor@123' });
    component.submit();

    expect(auth.login).toHaveBeenCalledWith({ email: 'doctor@clinic.com', password: 'Doctor@123' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/doctor/schedule');
  });

  it('returns to the page the user originally asked for', () => {
    const { component } = setup('/patient/book');
    auth.login.and.returnValue(of(authResponse('Patient')));

    component.form.setValue({ email: 'patient@clinic.com', password: 'Patient@123' });
    component.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/patient/book');
  });

  it('shows the API error and re-enables the button', () => {
    const { fixture, component, el } = setup();
    auth.login.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 401, error: { message: 'Email or password is incorrect.' } })));

    component.form.setValue({ email: 'patient@clinic.com', password: 'wrong' });
    component.submit();
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Email or password is incorrect.');
    expect(component.loading()).toBeFalse();
  });
});
