import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  CreateProductRequest,
  ProductResponse,
  UpdateProductRequest
} from '../../core/api/api.models';
import { GatewayApiError } from '../../core/api/gateway-error';
import { ProductApiService } from '../../core/api/product-api.service';

type ManagementState = 'loading' | 'ready' | 'error';
const PRODUCT_LIST_LIMIT = 100;
type ProductForm = FormGroup<{
  name: FormControl<string>;
  description: FormControl<string>;
  unitPrice: FormControl<number>;
  stock: FormControl<number>;
  isActive: FormControl<boolean>;
}>;

@Component({
  selector: 'app-product-management',
  imports: [DecimalPipe, ReactiveFormsModule, RouterLink],
  templateUrl: './product-management.component.html',
  styleUrl: './product-management.component.scss'
})
export class ProductManagementComponent implements OnInit {
  private readonly productApi = inject(ProductApiService);

  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly state = signal<ManagementState>('loading');
  protected readonly editingProduct = signal<ProductResponse | null>(null);
  protected readonly isEditing = computed(() => this.editingProduct() !== null);
  protected readonly isSaving = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  readonly form: ProductForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)]
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(2_000)]
    }),
    unitPrice: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)]
    }),
    stock: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)]
    }),
    isActive: new FormControl(true, { nonNullable: true })
  });

  ngOnInit(): void {
    this.loadProducts();
  }

  protected loadProducts(): void {
    this.state.set('loading');
    this.errorMessage.set('');

    this.productApi
      .list({ limit: PRODUCT_LIST_LIMIT, includeInactive: true })
      .subscribe({
        next: (page) => {
          this.products.set(page.items);
          this.state.set('ready');
        },
        error: (error: unknown) => {
          this.products.set([]);
          this.errorMessage.set(this.describeError(error));
          this.state.set('error');
        }
      });
  }

  startCreate(): void {
    this.editingProduct.set(null);
    this.form.reset({
      name: '',
      description: '',
      unitPrice: 0,
      stock: 0,
      isActive: true
    });
    this.clearMessages();
  }

  startEdit(product: ProductResponse): void {
    this.editingProduct.set(product);
    this.form.reset({
      name: product.name,
      description: product.description ?? '',
      unitPrice: product.unitPrice,
      stock: product.availableStock,
      isActive: product.isActive
    });
    this.clearMessages();
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.clearMessages();
    const value = this.form.getRawValue();
    const editingProduct = this.editingProduct();

    const request$ = editingProduct
      ? this.productApi.update(
          editingProduct.id,
          this.toUpdateRequest(value),
          editingProduct.version
        )
      : this.productApi.create(this.toCreateRequest(value));

    request$.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.successMessage.set(
          editingProduct ? 'Product updated.' : 'Product created.'
        );
        this.editingProduct.set(null);
        this.form.reset({
          name: '',
          description: '',
          unitPrice: 0,
          stock: 0,
          isActive: true
        });
        this.loadProducts();
      },
      error: (error: unknown) => {
        this.isSaving.set(false);
        this.handleSaveError(error);
      }
    });
  }

  toggleActive(product: ProductResponse): void {
    if (this.isSaving()) {
      return;
    }

    this.isSaving.set(true);
    this.clearMessages();
    this.productApi
      .update(product.id, { isActive: !product.isActive }, product.version)
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.successMessage.set(
            product.isActive ? 'Product deactivated.' : 'Product activated.'
          );
          this.loadProducts();
        },
        error: (error: unknown) => {
          this.isSaving.set(false);
          this.handleSaveError(error);
        }
      });
  }

  fieldError(field: string): string | null {
    const control = this.form.get(field);
    if (control?.hasError('required')) {
      return 'This field is required.';
    }
    if (control?.hasError('maxlength')) {
      return 'This value is too long.';
    }
    if (control?.hasError('min')) {
      return 'This value cannot be negative.';
    }
    return this.serverErrors()[field]?.join(' ') ?? null;
  }

  private toCreateRequest(value: ReturnType<ProductForm['getRawValue']>): CreateProductRequest {
    return {
      name: value.name.trim(),
      description: value.description,
      unitPrice: value.unitPrice,
      currency: 'VND',
      initialStock: value.stock,
      isActive: value.isActive
    };
  }

  private toUpdateRequest(value: ReturnType<ProductForm['getRawValue']>): UpdateProductRequest {
    return {
      name: value.name.trim(),
      description: value.description,
      unitPrice: value.unitPrice,
      availableStock: value.stock,
      isActive: value.isActive
    };
  }

  private handleSaveError(error: unknown): void {
    if (error instanceof GatewayApiError && error.code === 'VALIDATION_ERROR') {
      this.applyServerErrors(error.problem?.errors ?? {});
      this.errorMessage.set('Please correct the highlighted fields.');
      return;
    }

    if (
      error instanceof GatewayApiError &&
      error.code === 'PRODUCT_CONCURRENCY_CONFLICT'
    ) {
      this.errorMessage.set(
        'This Product changed elsewhere. Reload the list and edit the current version.'
      );
      return;
    }

    this.errorMessage.set(this.describeError(error));
  }

  private applyServerErrors(errors: Record<string, string[]>): void {
    const mappedErrors: Record<string, string[]> = {};
    for (const [field, messages] of Object.entries(errors)) {
      const controlName = field === 'initialStock' || field === 'availableStock'
        ? 'stock'
        : field;
      mappedErrors[controlName] = messages;
      const control = this.form.get(controlName);
      if (control) {
        control.setErrors({ ...control.errors, server: messages.join(' ') });
      }
    }
    this.serverErrors.set(mappedErrors);
  }

  private clearMessages(): void {
    this.errorMessage.set('');
    this.successMessage.set('');
    this.serverErrors.set({});
  }

  private describeError(error: unknown): string {
    if (error instanceof GatewayApiError && error.kind === 'connectivity') {
      return 'The Gateway is unavailable. Check the application services and retry.';
    }
    if (error instanceof GatewayApiError) {
      return error.message;
    }
    return 'The Product operation failed. Please retry.';
  }
}
