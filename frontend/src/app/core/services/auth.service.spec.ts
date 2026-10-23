import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { authResponse, storeSession } from '../../../testing/test-data';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;

  function setup(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    http?.verify();
    localStorage.clear();
  });

  it('starts signed out when nothing is stored', () => {
    const auth = setup();
    expect(auth.isLoggedIn()).toBeFalse();
    expect(auth.user()).toBeNull();
    expect(auth.token).toBeNull();
  });

  it('restores a stored session that has not expired', () => {
    storeSession('Doctor');
    const auth = setup();
    expect(auth.user()?.role).toBe('Doctor');
    expect(auth.token).toBe('token-for-doctor');
  });

  it('discards an expired session', () => {
    storeSession('Patient', { expiresAt: '2000-01-01T00:00:00Z' });
    const auth = setup();
    expect(auth.isLoggedIn()).toBeFalse();
    expect(localStorage.getItem('clinic_auth')).toBeNull();
  });

  it('ignores corrupted storage', () => {
    localStorage.setItem('clinic_auth', '{not json');
    expect(setup().isLoggedIn()).toBeFalse();
  });

  it('login posts credentials and stores the session', () => {
    const auth = setup();
    auth.login({ email: 'a@b.com', password: 'pw' }).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'a@b.com', password: 'pw' });
    req.flush(authResponse('Admin'));

    expect(auth.user()?.role).toBe('Admin');
    expect(JSON.parse(localStorage.getItem('clinic_auth')!).token).toBe('token-for-admin');
  });

  it('a failed login leaves the user signed out', () => {
    const auth = setup();
    auth.login({ email: 'a@b.com', password: 'bad' }).subscribe({ error: () => {} });

    http.expectOne(`${environment.apiUrl}/auth/login`)
      .flush({ message: 'Email or password is incorrect.' }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isLoggedIn()).toBeFalse();
  });

  it('register posts the form and signs the patient in', () => {
    const auth = setup();
    auth.register({ fullName: 'New', email: 'n@x.com', password: 'secret1' }).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/auth/register`);
    expect(req.request.method).toBe('POST');
    req.flush(authResponse('Patient'));

    expect(auth.user()?.role).toBe('Patient');
  });

  it('logout clears the session and goes to the sign-in page', () => {
    storeSession('Patient');
    const auth = setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    auth.logout();

    expect(auth.isLoggedIn()).toBeFalse();
    expect(localStorage.getItem('clinic_auth')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('runs sign-out hooks, such as closing the live connection', () => {
    storeSession('Doctor');
    const auth = setup();
    spyOn(TestBed.inject(Router), 'navigate');
    const hook = jasmine.createSpy('hook');
    auth.onLogout(hook);

    auth.logout();

    expect(hook).toHaveBeenCalled();
  });

  it('homeRoute sends each role to its own workspace', () => {
    const auth = setup();
    expect(auth.homeRoute('Admin')).toBe('/admin/dashboard');
    expect(auth.homeRoute('Doctor')).toBe('/doctor/schedule');
    expect(auth.homeRoute('Patient')).toBe('/patient/appointments');
    expect(auth.homeRoute(undefined)).toBe('/');
  });
});
