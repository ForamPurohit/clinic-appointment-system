import { Injectable, OnDestroy, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Observable, Subject, filter } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ScheduleChangedEvent, SlotsChangedEvent } from '../models/models';
import { AuthService } from './auth.service';

/**
 * One shared SignalR connection to the API.
 * - watchSlots(doctorId): emits when that doctor's availability changes.
 * - scheduleChanges$: emits for the signed-in doctor's own schedule.
 * The connection retries automatically; if real-time isn't available the app still works,
 * it just doesn't refresh on its own.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService implements OnDestroy {
  private auth = inject(AuthService);
  private connection?: HubConnection;
  private starting?: Promise<void>;
  private watchCounts = new Map<number, number>();

  private slotsSubject = new Subject<SlotsChangedEvent>();
  private scheduleSubject = new Subject<ScheduleChangedEvent>();

  readonly scheduleChanges$ = this.scheduleSubject.asObservable();

  constructor() {
    this.auth.onLogout(() => void this.disconnect());
  }

  watchSlots(doctorId: number): Observable<SlotsChangedEvent> {
    return new Observable<SlotsChangedEvent>(subscriber => {
      const sub = this.slotsSubject.pipe(filter(e => e.doctorId === doctorId)).subscribe(subscriber);
      this.addWatch(doctorId);
      return () => {
        sub.unsubscribe();
        this.removeWatch(doctorId);
      };
    });
  }

  /** Starts the connection, e.g. so a doctor receives schedule changes. */
  connect(): Promise<void> {
    if (!this.connection) {
      this.connection = new HubConnectionBuilder()
        .withUrl(environment.hubUrl, { accessTokenFactory: () => this.auth.token ?? '' })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      this.connection.on('SlotsChanged', (e: SlotsChangedEvent) => this.slotsSubject.next(e));
      this.connection.on('ScheduleChanged', (e: ScheduleChangedEvent) => this.scheduleSubject.next(e));

      // Groups don't survive a reconnect, so join them again
      this.connection.onreconnected(() => {
        this.watchCounts.forEach((_, doctorId) => this.invoke('WatchDoctor', doctorId));
      });
    }

    if (this.connection.state === HubConnectionState.Disconnected) {
      this.starting = this.connection.start().catch(err => console.warn('Live updates unavailable', err));
    }
    return this.starting ?? Promise.resolve();
  }

  /** Closes the connection (for example on sign-out, so the next user connects with their own token). */
  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = undefined;
    this.starting = undefined;
    this.watchCounts.clear();
    await connection?.stop();
  }

  ngOnDestroy() {
    void this.disconnect();
  }

  private addWatch(doctorId: number) {
    const count = this.watchCounts.get(doctorId) ?? 0;
    this.watchCounts.set(doctorId, count + 1);
    if (count === 0) {
      void this.connect().then(() => this.invoke('WatchDoctor', doctorId));
    }
  }

  private removeWatch(doctorId: number) {
    const count = (this.watchCounts.get(doctorId) ?? 1) - 1;
    if (count > 0) {
      this.watchCounts.set(doctorId, count);
      return;
    }
    this.watchCounts.delete(doctorId);
    this.invoke('UnwatchDoctor', doctorId);
  }

  private invoke(method: string, doctorId: number) {
    if (this.connection?.state === HubConnectionState.Connected) {
      this.connection.invoke(method, doctorId).catch(() => {});
    }
  }
}
