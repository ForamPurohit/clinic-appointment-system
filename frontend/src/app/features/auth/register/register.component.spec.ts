import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { authResponse } from '../../../../testing/test-data';
import { RegisterComponent } from './register.component';

describe('RegisterComponent (UC-01 Register)', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let router: Router;

  function setup() {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['register']);
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }

  const valid = {
    fullName: 'Asha Rao',
    email: 'asha@mail.com',
    phoneNumber: '',
    dateOfBirth: '',
    gender: '',
    password: 'secret1'
  };

  it('rejects a short password without calling the API', () => {
    const { component } = setup();
    component.form.setValue({ ...valid, password: '123' });

    component.submit();

    expect(auth.register).not.toHaveBeenCalled();
  });

  it('rejects a phone number with letters', () => {
    const { component } = setup();
    component.form.setValue({ ...valid, phoneNumber: 'call me' });

    expect(component.form.controls.phoneNumber.invalid).toBeTrue();
  });

  it('sends empty optional fields as null and goes to booking', () => {
    const { component } = setup();
    auth.register.and.returnValue(of(authResponse('Patient')));
    component.form.setValue(valid);

    component.submit();

    expect(auth.register).toHaveBeenCalledWith({
      fullName: 'Asha Rao',
      email: 'asha@mail.com',
      password: 'secret1',
      phoneNumber: null,
      dateOfBirth: null,
      gender: null
    });
    expect(router.navigate).toHaveBeenCalledWith(['/patient/book']);
  });

  it('shows a duplicate-email error from the API', () => {
    const { fixture, component, el } = setup();
    auth.register.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 409, error: { message: 'An account with this email already exists.' } })));
    component.form.setValue(valid);

    component.submit();
    fixture.detectChanges();

    expect(el.textContent).toContain('An account with this email already exists.');
  });
});
