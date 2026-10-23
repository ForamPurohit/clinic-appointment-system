import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { RealtimeService } from './realtime.service';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  it('shows a toast and removes it after the duration', fakeAsync(() => {
    const toast = TestBed.inject(ToastService);

    toast.show('Saved', 1000);
    expect(toast.toasts().map(t => t.text)).toEqual(['Saved']);

    tick(1000);
    expect(toast.toasts()).toEqual([]);
  }));

  it('keeps at most three toasts', fakeAsync(() => {
    const toast = TestBed.inject(ToastService);
    ['a', 'b', 'c', 'd'].forEach(t => toast.show(t, 1000));

    expect(toast.toasts().map(t => t.text)).toEqual(['b', 'c', 'd']);
    tick(1000);
  }));
});

describe('RealtimeService', () => {
  let service: RealtimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    service = TestBed.inject(RealtimeService);
    spyOn(service, 'connect').and.resolveTo();
  });

  // Reaches into the service to simulate messages from the hub
  const push = (doctorId: number, date: string) =>
    (service as unknown as { slotsSubject: { next(v: unknown): void } }).slotsSubject.next({ doctorId, date });

  it('only passes on changes for the watched doctor', () => {
    const seen: string[] = [];
    const sub = service.watchSlots(10).subscribe(e => seen.push(e.date));

    push(10, '2026-10-19');
    push(11, '2026-10-19');
    push(10, '2026-10-20');

    expect(seen).toEqual(['2026-10-19', '2026-10-20']);
    sub.unsubscribe();
  });

  it('connects once per doctor however many components watch it', () => {
    const a = service.watchSlots(10).subscribe();
    const b = service.watchSlots(10).subscribe();

    expect(service.connect).toHaveBeenCalledTimes(1);
    a.unsubscribe();
    b.unsubscribe();
  });
});
