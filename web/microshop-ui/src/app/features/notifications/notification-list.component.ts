import { CurrencyPipe, DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, merge, timer } from 'rxjs';
import {
  catchError,
  exhaustMap,
  finalize,
  map,
  take,
  tap
} from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { GatewayApiError } from '../../core/api/gateway-error';
import { NotificationApiService } from '../../core/api/notification-api.service';
import { NotificationResponse } from '../../core/api/api.models';

type NotificationListState = 'loading' | 'ready' | 'empty' | 'error';

const POLL_INTERVAL_MS = 15_000;
const POLL_MAX_ATTEMPTS = 5;
const PAGE_SIZE = 20;

@Component({
  selector: 'app-notification-list',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './notification-list.component.html',
  styleUrl: './notification-list.component.scss'
})
export class NotificationListComponent implements OnInit {
  private readonly notificationApi = inject(NotificationApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshRequests = new Subject<boolean>();

  protected readonly notifications = signal<NotificationResponse[]>([]);
  protected readonly state = signal<NotificationListState>('loading');
  protected readonly errorMessage = signal('');
  protected readonly actionError = signal('');
  protected readonly updatingId = signal<string | null>(null);
  protected readonly pollingMessage =
    `Automatic refresh checks run up to ${POLL_MAX_ATTEMPTS} times. Refresh manually to check again.`;

  ngOnInit(): void {
    const boundedPolling$ = timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS).pipe(
      take(POLL_MAX_ATTEMPTS - 1),
      map(() => false)
    );

    merge(this.refreshRequests, boundedPolling$)
      .pipe(
        exhaustMap((isManual) => {
          if (isManual) {
            this.state.set('loading');
            this.errorMessage.set('');
          }

          return this.notificationApi.list({ page: 1, limit: PAGE_SIZE }).pipe(
            tap((page) => {
              this.notifications.set(page.items);
              this.errorMessage.set('');
              this.state.set(page.items.length === 0 ? 'empty' : 'ready');
            }),
            catchError((error: unknown) => {
              const message = this.describeError(error);
              if (isManual || this.notifications().length === 0) {
                this.notifications.set([]);
                this.errorMessage.set(message);
                this.state.set('error');
              } else {
                this.actionError.set(`Automatic refresh failed: ${message}`);
              }
              return EMPTY;
            })
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();

    this.refreshRequests.next(false);
  }

  protected refresh(): void {
    this.actionError.set('');
    this.refreshRequests.next(true);
  }

  protected markAsRead(notification: NotificationResponse): void {
    if (notification.isRead || this.updatingId() !== null) {
      return;
    }

    this.actionError.set('');
    this.updatingId.set(notification.id);
    this.notificationApi
      .markAsRead(notification.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.updatingId.set(null))
      )
      .subscribe({
        next: (updated) => {
          this.notifications.update((items) =>
            items.map((item) => (item.id === updated.id ? updated : item))
          );
        },
        error: (error: unknown) => {
          this.actionError.set(this.describeError(error));
        }
      });
  }

  private describeError(error: unknown): string {
    if (error instanceof GatewayApiError && error.kind === 'connectivity') {
      return 'The Gateway is unavailable. Check the application services and retry.';
    }
    if (error instanceof GatewayApiError) {
      return error.message;
    }
    return 'Notifications could not be loaded. Please retry.';
  }
}
