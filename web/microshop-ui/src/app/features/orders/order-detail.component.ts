import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { GatewayApiError } from '../../core/api/gateway-error';
import { OrderApiService } from '../../core/api/order-api.service';
import { OrderResponse } from '../../core/api/api.models';

type DetailState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-order-detail',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss'
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly orderApi = inject(OrderApiService);

  protected readonly order = signal<OrderResponse | null>(null);
  protected readonly state = signal<DetailState>('loading');
  protected readonly errorMessage = signal('');
  protected readonly isCancelling = signal(false);

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (!orderId) {
      this.state.set('error');
      this.errorMessage.set('The order ID is missing.');
      return;
    }
    this.loadOrder(orderId);
  }

  protected loadOrder(orderId = this.route.snapshot.paramMap.get('orderId') ?? ''): void {
    if (!orderId) {
      return;
    }

    this.state.set('loading');
    this.errorMessage.set('');
    this.orderApi.get(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.state.set('ready');
      },
      error: (error: unknown) => {
        this.order.set(null);
        this.errorMessage.set(this.describeError(error));
        this.state.set('error');
      }
    });
  }

  protected cancelOrder(): void {
    const currentOrder = this.order();
    if (!currentOrder || !currentOrder.canCancel || this.isCancelling()) {
      return;
    }

    this.isCancelling.set(true);
    this.errorMessage.set('');
    this.orderApi.cancel(currentOrder.id).subscribe({
      next: (order) => {
        this.order.set(order);
        this.isCancelling.set(false);
      },
      error: (error: unknown) => {
        this.isCancelling.set(false);
        this.errorMessage.set(this.describeError(error));
      }
    });
  }

  private describeError(error: unknown): string {
    if (error instanceof GatewayApiError && error.kind === 'connectivity') {
      return 'The Gateway or Order Service is unavailable. Retry when it is healthy.';
    }
    if (error instanceof GatewayApiError) {
      return error.message;
    }
    return 'The order could not be loaded. Please retry.';
  }
}
