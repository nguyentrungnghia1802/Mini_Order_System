import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { GatewayApiError } from '../../core/api/gateway-error';
import { OrderApiService } from '../../core/api/order-api.service';
import { OrderResponse } from '../../core/api/api.models';

type OrderListState = 'loading' | 'ready' | 'empty' | 'error';

@Component({
  selector: 'app-order-list',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './order-list.component.html',
  styleUrl: './order-list.component.scss'
})
export class OrderListComponent implements OnInit {
  private readonly orderApi = inject(OrderApiService);

  protected readonly orders = signal<OrderResponse[]>([]);
  protected readonly state = signal<OrderListState>('loading');
  protected readonly errorMessage = signal('');

  ngOnInit(): void {
    this.loadOrders();
  }

  protected loadOrders(): void {
    this.state.set('loading');
    this.errorMessage.set('');

    this.orderApi.list().subscribe({
      next: (page) => {
        this.orders.set(page.items);
        this.state.set(page.items.length === 0 ? 'empty' : 'ready');
      },
      error: (error: unknown) => {
        this.orders.set([]);
        this.errorMessage.set(this.describeError(error));
        this.state.set('error');
      }
    });
  }

  private describeError(error: unknown): string {
    if (error instanceof GatewayApiError && error.kind === 'connectivity') {
      return 'The Gateway is unavailable. Check the application services and retry.';
    }
    if (error instanceof GatewayApiError) {
      return error.message;
    }
    return 'Orders could not be loaded. Please retry.';
  }
}
