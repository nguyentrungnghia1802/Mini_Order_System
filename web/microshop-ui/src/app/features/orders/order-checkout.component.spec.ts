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
import { OrderCheckoutComponent } from './order-checkout.component';

describe('OrderCheckoutComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderCheckoutComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('submits selected quantities and displays a confirmed result', () => {
    const fixture = TestBed.createComponent(OrderCheckoutComponent);
    fixture.detectChanges();
    flushProducts();

    const component = fixture.componentInstance;
    component.form.setValue({
      customerName: 'Nguyen Van A',
      customerEmail: 'a@example.com'
    });
    component.quantityControl('product-1').setValue(2);
    component.submit();
    component.submit();

    const request = httpTesting.expectOne('/api/orders');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      customerName: 'Nguyen Van A',
      customerEmail: 'a@example.com',
      items: [{ productId: 'product-1', quantity: 2 }]
    });
    expect(httpTesting.match('/api/orders')).toHaveLength(0);
    request.flush({
      id: 'order-1',
      status: 'confirmed',
      totalAmount: 2_400_000,
      currency: 'VND'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Order confirmed');
    expect(fixture.nativeElement.textContent).toContain('order-1');
  });

  it('displays a rejected result for known Product business failures', () => {
    const fixture = TestBed.createComponent(OrderCheckoutComponent);
    fixture.detectChanges();
    flushProducts();

    const component = fixture.componentInstance;
    component.form.setValue({
      customerName: 'Nguyen Van A',
      customerEmail: 'a@example.com'
    });
    component.quantityControl('product-1').setValue(2);
    component.submit();

    const request = httpTesting.expectOne('/api/orders');
    request.flush(
      { code: 'INSUFFICIENT_STOCK', detail: 'Not enough stock.' },
      { status: 409, statusText: 'Conflict' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Order rejected');
    expect(fixture.nativeElement.textContent).toContain('Not enough stock.');
  });

  it('displays a dependency outcome for Product Service failures', () => {
    const fixture = TestBed.createComponent(OrderCheckoutComponent);
    fixture.detectChanges();
    flushProducts();

    const component = fixture.componentInstance;
    component.form.setValue({
      customerName: 'Nguyen Van A',
      customerEmail: 'a@example.com'
    });
    component.quantityControl('product-1').setValue(1);
    component.submit();

    const request = httpTesting.expectOne('/api/orders');
    request.flush(
      { code: 'PRODUCT_SERVICE_UNAVAILABLE', detail: 'Product is offline.' },
      { status: 503, statusText: 'Service Unavailable' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Order status is unknown');
    expect(fixture.nativeElement.textContent).toContain('Product is offline.');
  });

  function flushProducts(): void {
    const request = httpTesting.expectOne('/api/products?limit=100');
    request.flush({
      items: [
        {
          id: 'product-1',
          name: 'Keyboard',
          description: 'Demo keyboard',
          unitPrice: 1_200_000,
          currency: 'VND',
          availableStock: 8,
          isActive: true,
          createdAtUtc: '2026-08-18T00:00:00Z',
          updatedAtUtc: '2026-08-18T00:00:00Z',
          version: 1
        }
      ],
      page: 1,
      limit: 20,
      total: 1,
      totalPages: 1
    });
  }
});
