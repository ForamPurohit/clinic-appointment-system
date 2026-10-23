import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { storeSession } from '../../../testing/test-data';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  function setup() {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('adds the bearer token when signed in', () => {
    storeSession('Patient');
    setup();

    http.get('/api/anything').subscribe();

    const req = controller.expectOne('/api/anything');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-for-patient');
    req.flush({});
  });

  it('sends no Authorization header when signed out', () => {
    setup();

    http.get('/api/anything').subscribe();

    const req = controller.expectOne('/api/anything');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('signs the user out when the API rejects their token', () => {
    storeSession('Patient');
    setup();
    const logout = spyOn(auth, 'logout');
    let failed = false;

    http.get('/api/anything').subscribe({ error: () => (failed = true) });
    controller.expectOne('/api/anything').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(logout).toHaveBeenCalled();
    expect(failed).toBeTrue();
  });

  it('does not sign out on 401 when there was no token (a failed sign-in)', () => {
    setup();
    const logout = spyOn(auth, 'logout');

    http.post('/api/auth/login', {}).subscribe({ error: () => {} });
    controller.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(logout).not.toHaveBeenCalled();
  });

  it('does not sign out on other errors such as 403', () => {
    storeSession('Patient');
    setup();
    const logout = spyOn(auth, 'logout');

    http.get('/api/admin/stats').subscribe({ error: () => {} });
    controller.expectOne('/api/admin/stats').flush({}, { status: 403, statusText: 'Forbidden' });

    expect(logout).not.toHaveBeenCalled();
  });
});
