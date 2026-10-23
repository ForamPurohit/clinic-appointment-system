import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DoctorLeave, TimeSlot } from '../../core/models/models';
import { DoctorService } from '../../core/services/doctor.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { FakeRealtimeService, MONDAY_8AM, doctor, slots } from '../../../testing/test-data';
import { SlotPickerComponent } from './slot-picker.component';

describe('SlotPickerComponent (UC-03 open times, UC-15 leave, UC-18 live updates)', () => {
  let fixture: ComponentFixture<SlotPickerComponent>;
  let component: SlotPickerComponent;
  let el: HTMLElement;
  let doctors: jasmine.SpyObj<DoctorService>;
  let realtime: FakeRealtimeService;
  let emitted: (TimeSlot | null)[];

  function setup(leaves: DoctorLeave[] = [], currentStart: string | null = null) {
    doctors = jasmine.createSpyObj<DoctorService>('DoctorService', ['getLeaves', 'getSlots']);
    doctors.getLeaves.and.returnValue(of(leaves));
    doctors.getSlots.and.callFake((_id, date) => of(slots(date)));
    realtime = new FakeRealtimeService();

    TestBed.configureTestingModule({
      imports: [SlotPickerComponent],
      providers: [
        { provide: DoctorService, useValue: doctors },
        { provide: RealtimeService, useValue: realtime }
      ]
    });
    fixture = TestBed.createComponent(SlotPickerComponent);
    component = fixture.componentInstance;
    el = fixture.nativeElement;
    emitted = [];
    component.slotChange.subscribe(s => emitted.push(s));
    fixture.componentRef.setInput('doctor', doctor());
    fixture.componentRef.setInput('currentStart', currentStart);
    fixture.detectChanges();
  }

  beforeEach(() => {
    jasmine.clock().install();
    jasmine.clock().mockDate(MONDAY_8AM);
  });

  afterEach(() => jasmine.clock().uninstall());

  it('opens the first working day and loads its slots', () => {
    setup();
    expect(component.selectedDate()).toBe('2026-10-19');
    expect(doctors.getSlots).toHaveBeenCalledWith(10, '2026-10-19');
  });

  it('shows 14 days and disables weekends', () => {
    setup();
    const days = Array.from(el.querySelectorAll<HTMLButtonElement>('.day'));
    expect(days.length).toBe(14);
    expect(days.slice(0, 5).every(d => !d.disabled)).toBeTrue();
    expect(days[5].disabled).toBeTrue();
    expect(days[6].disabled).toBeTrue();
  });

  it('marks leave days and skips them when choosing the first day', () => {
    setup([{ id: 1, doctorId: 10, startDate: '2026-10-19', endDate: '2026-10-20', reason: null }]);

    const days = Array.from(el.querySelectorAll<HTMLButtonElement>('.day'));
    expect(days[0].disabled).toBeTrue();
    expect(days[0].textContent).toContain('Leave');
    expect(days[1].classList).toContain('day--leave');
    expect(component.selectedDate()).toBe('2026-10-21');
  });

  it('ignores clicks on unavailable days', () => {
    setup();
    doctors.getSlots.calls.reset();

    component.chooseDate(component.days()[5]);

    expect(doctors.getSlots).not.toHaveBeenCalled();
  });

  it('disables taken times and counts open ones', () => {
    setup();
    const buttons = Array.from(el.querySelectorAll<HTMLButtonElement>('.slot'));
    expect(buttons[0].disabled).toBeTrue();
    expect(el.textContent).toContain('3 of 4 times open');
  });

  it('emits the chosen slot, and null when the day changes', () => {
    setup();
    const open = component.slots()[1];

    component.chooseSlot(open);
    component.chooseDate(component.days()[1]);

    expect(emitted).toContain(open);
    expect(emitted[emitted.length - 1]).toBeNull();
  });

  it('cannot choose a taken slot', () => {
    setup();
    component.chooseSlot(component.slots()[0]);
    expect(component.selectedSlot()).toBeNull();
  });

  it('marks the appointment being rescheduled as "current" and not selectable', () => {
    setup([], '2026-10-19T09:30:00');
    const current = component.slots()[1];

    expect(component.isCurrent(current)).toBeTrue();
    component.chooseSlot(current);
    expect(component.selectedSlot()).toBeNull();
    expect(el.querySelector('.slot--current')?.textContent).toContain('current');
    expect(el.textContent).toContain('2 of 4 times open');
  });

  it('watches the doctor for live changes', () => {
    setup();
    expect(realtime.watched).toEqual([10]);
  });

  it('reloads the shown day when another patient books, and drops a slot that was taken', () => {
    setup();
    component.chooseSlot(component.slots()[2]); // 10:00
    doctors.getSlots.and.callFake((_id, date) => of(slots(date, ['09:00', '10:00'])));

    realtime.emitSlots({ doctorId: 10, date: '2026-10-19' });
    fixture.detectChanges();

    expect(component.selectedSlot()).toBeNull();
    expect(emitted[emitted.length - 1]).toBeNull();
    expect(el.querySelector('.live-note')?.textContent).toContain('Updated just now');
  });

  it('ignores live changes for other days', () => {
    setup();
    doctors.getSlots.calls.reset();

    realtime.emitSlots({ doctorId: 10, date: '2026-10-22' });

    expect(doctors.getSlots).not.toHaveBeenCalled();
  });

  it('refresh() clears the choice and reloads', () => {
    setup();
    component.chooseSlot(component.slots()[1]);
    doctors.getSlots.calls.reset();

    component.refresh();

    expect(component.selectedSlot()).toBeNull();
    expect(doctors.getSlots).toHaveBeenCalledTimes(1);
  });

  it('starts over when given a different doctor', () => {
    setup();
    fixture.componentRef.setInput('doctor', doctor({ id: 11, workingDays: [3] }));
    fixture.detectChanges();

    expect(component.selectedDate()).toBe('2026-10-21');
    expect(realtime.watched).toEqual([10, 11]);
  });
});
