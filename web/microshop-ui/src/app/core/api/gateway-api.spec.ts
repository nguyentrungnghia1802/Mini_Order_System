import {
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CreateOrderRequest } from './api.models';
import { GATEWAY_API_PATHS } from './api.paths';
import { gatewayErrorInterceptor } from './gateway-error.interceptor';
import { GatewayApiError } from './gateway-error';
import { OrderApiService } from './order-api.service';
import { ProductApiService } from './product-api.service';
import { NotificationApiService } from './notification-api.service';

describe('Gateway API clients', () => {
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    });
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('uses a same-origin Gateway path for Product listing', () => {
    const service = TestBed.inject(ProductApiService);
    let receivedPage = false;

    service.list({ page: 2, limit: 10, includeInactive: false }).subscribe(() => {
      receivedPage = true;
    });

    const request = httpTesting.expectOne(
      '/api/products?page=2&limit=10&includeInactive=false'
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.urlWithParams).toBe(
      '/api/products?page=2&limit=10&includeInactive=false'
    );
    request.flush({ items: [], page: 2, limit: 10, total: 0, totalPages: 0 });

    expect(receivedPage).toBe(true);
  });

  it('uses same-origin Gateway paths for Order create and cancellation', () => {
    const service = TestBed.inject(OrderApiService);
    const orderId = '11111111-1111-1111-1111-111111111111';
    const requestBody: CreateOrderRequest = {
      customerName: 'Nguyen Van A',
      customerEmail: 'a@example.com',
      items: [{ productId: orderId, quantity: 1 }]
    };

    service.create(requestBody).subscribe();
    const createRequest = httpTesting.expectOne(GATEWAY_API_PATHS.orders);
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.url).toBe('/api/orders');
    expect(createRequest.request.body).toEqual(requestBody);
    createRequest.flush({ id: orderId });

    service.cancel(orderId).subscribe();
    const cancelRequest = httpTesting.expectOne(
      `/api/orders/${orderId}/cancel`
    );
    expect(cancelRequest.request.method).toBe('POST');
    expect(cancelRequest.request.url).toBe(`/api/orders/${orderId}/cancel`);
    cancelRequest.flush({ id: orderId });
  });

  it('maps Gateway connectivity failures to a stable frontend error', () => {
    const service = TestBed.inject(ProductApiService);
    let receivedError: unknown;

    service.list().subscribe({
      error: (error: unknown) => {
        receivedError = error;
      }
    });

    const request = httpTesting.expectOne('/api/products');
    request.flush(
      {
        code: 'DOWNSTREAM_UNAVAILABLE',
        detail: 'The requested downstream service is unavailable.'
      },
      { status: 502, statusText: 'Bad Gateway' }
    );

    expect(receivedError).toBeInstanceOf(GatewayApiError);
    const gatewayError = receivedError as GatewayApiError;
    expect(gatewayError.kind).toBe('connectivity');
    expect(gatewayError.code).toBe('DOWNSTREAM_UNAVAILABLE');
  });

  it('uses same-origin Gateway paths for Notification listing and mark-as-read', () => {
    const service = TestBed.inject(NotificationApiService);
    const notificationId = 'notification-1';

    service.list({ page: 2, limit: 5, customerEmail: 'a@example.com' }).subscribe();
    const listRequest = httpTesting.expectOne(
      '/api/notifications?page=2&limit=5&customerEmail=a@example.com'
    );
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({
      items: [],
      page: 2,
      limit: 5,
      total: 0,
      totalPages: 0
    });

    service.markAsRead(notificationId).subscribe();
    const readRequest = httpTesting.expectOne(
      `/api/notifications/${notificationId}/read`
    );
    expect(readRequest.request.method).toBe('POST');
    expect(readRequest.request.body).toBeNull();
    readRequest.flush({ id: notificationId, isRead: true });
  });
});
