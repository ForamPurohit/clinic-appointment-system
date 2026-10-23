import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { DoctorLeave } from '../../../core/models/models';
import { DoctorService } from '../../../core/services/doctor.service';
import { MONDAY_8AM } from '../../../../testing/test-data';
import { LeaveComponent } from './leave.component';

describe('LeaveComponent (UC-15 Manage time off)', () => {
  let fixture: ComponentFixture<LeaveComponent>;
  let component: LeaveComponent;
  let el: HTMLElement;
  let doctors: jasmine.SpyObj<DoctorService>;

  const conference: DoctorLeave = { id: 1, doctorId: 10, startDate: '2026-10-28', endDate: '2026-10-30', reason: 'Conference' };

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(MONDAY_8AM);
    doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getMyLeaves', 'addLeave', 'deleteLeave']);
    doctors.getMyLeaves.and.returnValue(of([conference]));
    TestBed.configureTestingModule({ imports: [LeaveComponent], providers: [{ provide: DoctorService, useValue: doctors }] });
    fixture = TestBed.createComponent(LeaveComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => jasmine.clock().uninstall());

  it('lists upcoming leave with its length', () => {
    expect(el.querySelector('.leave-list')?.textContent).toContain('3 days, Conference');
    expect(component.today).toBe('2026-10-19');
  });

  it('moves the last day forward when the first day is set after it', () => {
    component.endDate = '2026-10-20';
    component.onStartChange('2026-10-22');
    expect(component.endDate).toBe('2026-10-22');
  });

  it('validates the dates before calling the API', () => {
    component.add();
    expect(component.error()).toContain('Choose the first and last day');

    component.startDate = '2026-10-25';
    component.endDate = '2026-10-24';
    component.add();
    expect(component.error()).toContain("can't be before");
    expect(doctors.addLeave).not.toHaveBeenCalled();
  });

  it('adds leave, keeps the list in date order and clears the form', () => {
    doctors.addLeave.and.returnValue(of({ id: 2, doctorId: 10, startDate: '2026-10-21', endDate: '2026-10-21', reason: null }));
    component.startDate = '2026-10-21';
    component.endDate = '2026-10-21';
    component.reason = '   ';

    component.add();

    expect(doctors.addLeave).toHaveBeenCalledWith({ startDate: '2026-10-21', endDate: '2026-10-21', reason: null });
    expect(component.leaves().map(l => l.id)).toEqual([2, 1]);
    expect(component.startDate).toBe('');
  });

  it('shows the conflict when patients are already booked', () => {
    doctors.addLeave.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { message: 'You have 2 scheduled appointments in this period.' }
    })));
    component.startDate = '2026-10-20';
    component.endDate = '2026-10-21';

    component.add();
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('2 scheduled appointments');
  });

  it('removes leave after confirmation', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    doctors.deleteLeave.and.returnValue(of(undefined));

    component.remove(conference);

    expect(doctors.deleteLeave).toHaveBeenCalledWith(1);
    expect(component.leaves()).toEqual([]);
  });
});
