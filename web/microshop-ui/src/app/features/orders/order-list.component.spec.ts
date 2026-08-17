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
import { OrderListComponent } from './order-list.component';

describe('OrderListComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderListComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('renders the paginated Order list returned by Gateway', () => {
    const fixture = TestBed.createComponent(OrderListComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/orders');
    request.flush({
      items: [
        {
          id: 'order-1',
          customerName: 'Nguyen Van A',
          customerEmail: 'a@example.com',
          status: 'confirmed',
          currency: 'VND',
          totalAmount: 2_400_000,
          items: [],
          canCancel: true,
          failureCode: null,
          failureDetail: null,
          createdAtUtc: '2026-08-18T00:00:00Z',
          updatedAtUtc: '2026-08-18T00:00:00Z',
          confirmedAtUtc: '2026-08-18T00:00:01Z',
          cancelledAtUtc: null,
          version: 2
        }
      ],
      page: 1,
      limit: 20,
      total: 1,
      totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('order-1');
    expect(fixture.nativeElement.textContent).toContain('confirmed');
    expect(fixture.nativeElement.querySelector('a.order-card')?.getAttribute('href')).toBe('/orders/order-1');
  });
});
