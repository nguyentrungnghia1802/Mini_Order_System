import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { GatewayApiError } from '../../core/api/gateway-error';
import { ProductApiService } from '../../core/api/product-api.service';
import { ProductResponse } from '../../core/api/api.models';

type CatalogState = 'loading' | 'ready' | 'empty' | 'error';
const PRODUCT_LIST_LIMIT = 100;

@Component({
  selector: 'app-product-catalog',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './product-catalog.component.html',
  styleUrl: './product-catalog.component.scss'
})
export class ProductCatalogComponent implements OnInit {
  private readonly productApi = inject(ProductApiService);

  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly state = signal<CatalogState>('loading');
  protected readonly errorMessage = signal('');

  ngOnInit(): void {
    this.loadProducts();
  }

  protected loadProducts(): void {
    this.state.set('loading');
    this.errorMessage.set('');

    this.productApi.list({ limit: PRODUCT_LIST_LIMIT }).subscribe({
      next: (page) => {
        this.products.set(page.items);
        this.state.set(page.items.length === 0 ? 'empty' : 'ready');
      },
      error: (error: unknown) => {
        this.products.set([]);
        this.errorMessage.set(this.describeError(error));
        this.state.set('error');
      }
    });
  }

  private describeError(error: unknown): string {
    if (error instanceof GatewayApiError && error.kind === 'connectivity') {
      return 'The Gateway is unavailable. Check that the Gateway and Product Service are running, then retry.';
    }

    if (error instanceof GatewayApiError) {
      return error.message;
    }

    return 'Products could not be loaded. Please retry.';
  }
}
