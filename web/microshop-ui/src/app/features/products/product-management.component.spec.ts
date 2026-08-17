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

import { ProductManagementComponent } from './product-management.component';
import { gatewayErrorInterceptor } from '../../core/api/gateway-error.interceptor';

describe('ProductManagementComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductManagementComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([gatewayErrorInterceptor])),
        provideHttpClientTesting()
      ]
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('creates a Product with the authoritative API request shape', () => {
    const fixture = TestBed.createComponent(ProductManagementComponent);
    fixture.detectChanges();
    flushProductList([]);

    const component = fixture.componentInstance;
    component.form.setValue({
      name: 'Keyboard',
      description: 'Demo keyboard',
      unitPrice: 1_200_000,
      stock: 8,
      isActive: true
    });
    component.submit();

    const createRequest = httpTesting.expectOne('/api/products');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual({
      name: 'Keyboard',
      description: 'Demo keyboard',
      unitPrice: 1_200_000,
      currency: 'VND',
      initialStock: 8,
      isActive: true
    });
    createRequest.flush({ id: 'product-1' });
    flushProductList([]);

    expect(component.successMessage()).toBe('Product created.');
  });

  it('updates with the current Product version and maps activation actions', () => {
    const product = productFixture(true);
    const fixture = TestBed.createComponent(ProductManagementComponent);
    fixture.detectChanges();
    flushProductList([product]);

    const component = fixture.componentInstance;
    component.startEdit(product);
    component.form.controls.stock.setValue(7);
    component.submit();

    const updateRequest = httpTesting.expectOne('/api/products/product-1');
    expect(updateRequest.request.method).toBe('PATCH');
    expect(updateRequest.request.headers.get('If-Match')).toBe('"3"');
    expect(updateRequest.request.body).toEqual({
      name: 'Keyboard',
      description: 'Demo keyboard',
      unitPrice: 1_200_000,
      availableStock: 7,
      isActive: true
    });
    updateRequest.flush({ ...product, availableStock: 7, version: 4 });
    flushProductList([{ ...product, availableStock: 7, version: 4 }]);

    component.toggleActive({ ...product, availableStock: 7, version: 4 });
    const activationRequest = httpTesting.expectOne('/api/products/product-1');
    expect(activationRequest.request.headers.get('If-Match')).toBe('"4"');
    expect(activationRequest.request.body).toEqual({ isActive: false });
    activationRequest.flush({ ...product, isActive: false, version: 5 });
    flushProductList([{ ...product, isActive: false, version: 5 }]);

    expect(component.successMessage()).toBe('Product deactivated.');
  });

  it('maps server validation errors to the matching form control', () => {
    const fixture = TestBed.createComponent(ProductManagementComponent);
    fixture.detectChanges();
    flushProductList([]);

    const component = fixture.componentInstance;
    component.form.patchValue({ name: 'Keyboard', unitPrice: 1, stock: 1 });
    component.submit();

    const request = httpTesting.expectOne('/api/products');
    request.flush(
      {
        code: 'VALIDATION_ERROR',
        detail: 'The product request is invalid.',
        errors: { name: ['Name is already used.'] }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(component.fieldError('name')).toContain('Name is already used.');
    expect(component.errorMessage()).toBe('Please correct the highlighted fields.');
  });

  function flushProductList(products: unknown[]): void {
    const request = httpTesting.expectOne(
      '/api/products?includeInactive=true'
    );
    request.flush({
      items: products,
      page: 1,
      limit: 20,
      total: products.length,
      totalPages: products.length === 0 ? 0 : 1
    });
  }

  function productFixture(isActive: boolean) {
    return {
      id: 'product-1',
      name: 'Keyboard',
      description: 'Demo keyboard',
      unitPrice: 1_200_000,
      currency: 'VND',
      availableStock: 8,
      isActive,
      createdAtUtc: '2026-08-18T00:00:00Z',
      updatedAtUtc: '2026-08-18T00:00:00Z',
      version: 3
    };
  }
});
