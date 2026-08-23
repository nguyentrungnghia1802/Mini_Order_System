import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  CreateOrderRequest,
  OrderResponse,
  ProductResponse
} from '../../core/api/api.models';
import { GatewayApiError } from '../../core/api/gateway-error';
import { OrderApiService } from '../../core/api/order-api.service';
import { ProductApiService } from '../../core/api/product-api.service';

type CheckoutState = 'loading' | 'ready' | 'empty' | 'error';
type CheckoutOutcome = 'none' | 'confirmed' | 'rejected' | 'dependency' | 'error';
const PRODUCT_LIST_LIMIT = 100;

@Component({
  selector: 'app-order-checkout',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink],
  templateUrl: './order-checkout.component.html',
  styleUrl: './order-checkout.component.scss'
})
export class OrderCheckoutComponent implements OnInit {
  private readonly productApi = inject(ProductApiService);
  private readonly orderApi = inject(OrderApiService);

  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly state = signal<CheckoutState>('loading');
  protected readonly outcome = signal<CheckoutOutcome>('none');
  protected readonly submittedOrder = signal<OrderResponse | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly isSubmitting = signal(false);

  readonly form = new FormGroup({
    customerName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(150)]
    }),
    customerEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(320)]
    })
  });

  private readonly quantityControls = new Map<string, FormControl<number>>();

  ngOnInit(): void {
    this.loadProducts();
  }

  protected loadProducts(): void {
    this.state.set('loading');
    this.errorMessage.set('');

    this.productApi.list({ limit: PRODUCT_LIST_LIMIT }).subscribe({
      next: (page) => {
        this.products.set(page.items);
        this.configureQuantityControls(page.items);
        this.state.set(page.items.length === 0 ? 'empty' : 'ready');
      },
      error: (error: unknown) => {
        this.products.set([]);
        this.state.set('error');
        this.errorMessage.set(this.describeError(error));
      }
    });
  }

  quantityControl(productId: string): FormControl<number> {
    return this.quantityControls.get(productId)!;
  }

  submit(): void {
    if (this.isSubmitting()) {
      return;
    }

    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errorMessage.set('Please enter a valid customer name and email.');
      this.outcome.set('error');
      return;
    }

    const items = this.products()
      .map((product) => ({
        productId: product.id,
        quantity: this.quantityControl(product.id).value
      }))
      .filter((item) => item.quantity > 0);

    if (items.length === 0) {
      this.errorMessage.set('Select at least one Product quantity.');
      this.outcome.set('error');
      return;
    }

    const value = this.form.getRawValue();
    const request: CreateOrderRequest = {
      customerName: value.customerName.trim(),
      customerEmail: value.customerEmail.trim(),
      items
    };

    this.isSubmitting.set(true);
    this.errorMessage.set('');
    this.outcome.set('none');
    this.orderApi.create(request).subscribe({
      next: (order) => {
        this.isSubmitting.set(false);
        this.submittedOrder.set(order);
        this.outcome.set(order.status === 'confirmed' ? 'confirmed' : 'rejected');
      },
      error: (error: unknown) => {
        this.isSubmitting.set(false);
        this.submittedOrder.set(null);
        this.outcome.set(this.classifyFailure(error));
        this.errorMessage.set(this.describeError(error));
      }
    });
  }

  protected productError(field: 'customerName' | 'customerEmail'): string | null {
    const control = this.form.controls[field];
    if (control.hasError('required')) {
      return 'This field is required.';
    }
    if (control.hasError('email')) {
      return 'Enter a valid email address.';
    }
    if (control.hasError('maxlength')) {
      return 'This value is too long.';
    }
    return null;
  }

  private configureQuantityControls(products: ProductResponse[]): void {
    this.quantityControls.clear();
    for (const product of products) {
      this.quantityControls.set(
        product.id,
        new FormControl(0, {
          nonNullable: true,
          validators: [Validators.min(0), Validators.max(100)]
        })
      );
    }
  }

  private classifyFailure(error: unknown): CheckoutOutcome {
    if (error instanceof GatewayApiError) {
      if (
        error.kind === 'connectivity' ||
        error.code === 'PRODUCT_SERVICE_UNAVAILABLE' ||
        error.code === 'INVENTORY_OUTCOME_UNKNOWN'
      ) {
        return 'dependency';
      }
      if (
        error.code === 'PRODUCT_NOT_FOUND' ||
        error.code === 'PRODUCT_INACTIVE' ||
        error.code === 'INSUFFICIENT_STOCK'
      ) {
        return 'rejected';
      }
    }
    return 'error';
  }

  private describeError(error: unknown): string {
    if (error instanceof GatewayApiError && error.kind === 'connectivity') {
      if (error.code === 'PRODUCT_SERVICE_UNAVAILABLE') {
        return error.message;
      }
      return 'The Gateway or a required service is unavailable. No order was confirmed.';
    }
    if (error instanceof GatewayApiError) {
      return error.message;
    }
    return 'The order could not be submitted. Please retry.';
  }
}
