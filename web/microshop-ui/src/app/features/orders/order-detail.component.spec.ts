import {
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';

import { ActivatedRoute } from '@angular/router';
import { gatewayErrorInterceptor } from '../../core/api/gateway-error.interceptor';
import { OrderDetailComponent } from './order-detail.component';

describe('OrderDetailComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ orderId: 'order-1' })
            }
          }
        },
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('loads order detail and cancels a confirmed Order once', () => {
    const fixture = TestBed.createComponent(OrderDetailComponent);
    fixture.detectChanges();

    const getRequest = httpTesting.expectOne('/api/orders/order-1');
    getRequest.flush(orderFixture('confirmed', true));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Order order-1');
    const cancelButton = fixture.nativeElement.querySelector('button.danger') as HTMLButtonElement;
    expect(cancelButton).toBeTruthy();
    cancelButton.click();

    const cancelRequest = httpTesting.expectOne('/api/orders/order-1/cancel');
    expect(cancelRequest.request.method).toBe('POST');
    cancelRequest.flush(orderFixture('cancelled', false));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('cancelled');
    expect(fixture.nativeElement.querySelector('button.danger')).toBeNull();
  });

  function orderFixture(status: 'confirmed' | 'cancelled', canCancel: boolean) {
    return {
      id: 'order-1',
      customerName: 'Nguyen Van A',
      customerEmail: 'a@example.com',
      status,
      currency: 'VND',
      totalAmount: 2_400_000,
      items: [
        {
          productId: 'product-1',
          productName: 'Keyboard',
          unitPrice: 1_200_000,
          quantity: 2,
          subtotal: 2_400_000
        }
      ],
      canCancel,
      failureCode: null,
      failureDetail: null,
      createdAtUtc: '2026-08-18T00:00:00Z',
      updatedAtUtc: '2026-08-18T00:00:00Z',
      confirmedAtUtc: '2026-08-18T00:00:01Z',
      cancelledAtUtc: status === 'cancelled' ? '2026-08-18T00:01:00Z' : null,
      version: 2
    };
  }
});
