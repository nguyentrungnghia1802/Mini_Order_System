import {
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { gatewayErrorInterceptor } from '../../core/api/gateway-error.interceptor';
import { NotificationListComponent } from './notification-list.component';

describe('NotificationListComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationListComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('renders eventual notifications and marks an unread item as read', () => {
    const fixture = TestBed.createComponent(NotificationListComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/notifications?page=1&limit=20');
    request.flush({
      items: [
        {
          id: 'notification-1',
          orderId: 'order-1',
          customerEmail: 'a@example.com',
          subject: 'Order confirmed',
          body: 'Your order order-1 was confirmed for 250,000.00 VND.',
          totalAmount: 250_000,
          currency: 'VND',
          isRead: false,
          createdAtUtc: '2026-08-18T00:00:03Z'
        }
      ],
      page: 1,
      limit: 20,
      total: 1,
      totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Order confirmed');
    expect(fixture.nativeElement.textContent).toContain('a@example.com');
    expect(fixture.nativeElement.querySelector('.unread-status')).toBeTruthy();

    const markReadButton = fixture.nativeElement.querySelector(
      'button.secondary'
    ) as HTMLButtonElement;
    markReadButton.click();
    const readRequest = httpTesting.expectOne(
      '/api/notifications/notification-1/read'
    );
    readRequest.flush({
      id: 'notification-1',
      orderId: 'order-1',
      customerEmail: 'a@example.com',
      subject: 'Order confirmed',
      body: 'Your order order-1 was confirmed for 250,000.00 VND.',
      totalAmount: 250_000,
      currency: 'VND',
      isRead: true,
      createdAtUtc: '2026-08-18T00:00:03Z'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Read');
    expect(fixture.nativeElement.querySelector('button.secondary')).toBeNull();
  });

  it('renders an empty state while eventual delivery is still pending', () => {
    const fixture = TestBed.createComponent(NotificationListComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/notifications?page=1&limit=20');
    request.flush({ items: [], page: 1, limit: 20, total: 0, totalPages: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No notifications yet.');
    expect(fixture.nativeElement.textContent).toContain('Refresh after');
  });

  it('renders a retryable error when Gateway is unavailable', () => {
    const fixture = TestBed.createComponent(NotificationListComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/notifications?page=1&limit=20');
    request.flush(
      { code: 'DOWNSTREAM_UNAVAILABLE' },
      { status: 502, statusText: 'Bad Gateway' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Notifications unavailable');
    expect(fixture.nativeElement.textContent).toContain('Gateway is unavailable');
    expect(fixture.nativeElement.querySelector('button')).toBeTruthy();
  });

  it('stops automatic polling after the bounded number of checks', () => {
    vi.useFakeTimers();
    try {
      const fixture = TestBed.createComponent(NotificationListComponent);
      fixture.detectChanges();

      for (let attempt = 0; attempt < 5; attempt++) {
        const request = httpTesting.expectOne('/api/notifications?page=1&limit=20');
        request.flush({ items: [], page: 1, limit: 20, total: 0, totalPages: 0 });
        fixture.detectChanges();
        if (attempt < 4) {
          vi.advanceTimersByTime(15_000);
        }
      }

      vi.advanceTimersByTime(15_000);
      expect(httpTesting.match('/api/notifications?page=1&limit=20')).toHaveLength(0);
    } finally {
      vi.useRealTimers();
    }
  });
});
