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

import { ProductCatalogComponent } from './product-catalog.component';
import { gatewayErrorInterceptor } from '../../core/api/gateway-error.interceptor';

describe('ProductCatalogComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductCatalogComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('renders the active Product list returned by Gateway', () => {
    const fixture = TestBed.createComponent(ProductCatalogComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/products');
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
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Keyboard');
    expect(fixture.nativeElement.textContent).toContain('8 in stock');
    expect(fixture.nativeElement.querySelector('.status.active')).toBeTruthy();
  });

  it('renders an empty state when Gateway returns no active Products', () => {
    const fixture = TestBed.createComponent(ProductCatalogComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/products');
    request.flush({ items: [], page: 1, limit: 20, total: 0, totalPages: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No active Products yet.');
  });

  it('renders a retryable error state when Gateway is unavailable', () => {
    const fixture = TestBed.createComponent(ProductCatalogComponent);
    fixture.detectChanges();

    const request = httpTesting.expectOne('/api/products');
    request.flush(
      { code: 'DOWNSTREAM_UNAVAILABLE' },
      { status: 502, statusText: 'Bad Gateway' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Catalog unavailable');
    expect(fixture.nativeElement.textContent).toContain('Gateway is unavailable');
    expect(fixture.nativeElement.querySelector('button')).toBeTruthy();
  });
});
