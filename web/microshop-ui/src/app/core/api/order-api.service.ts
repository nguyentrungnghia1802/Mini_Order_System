import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CreateOrderRequest,
  OrderListQuery,
  OrderPage,
  OrderResponse
} from './api.models';
import {
  GATEWAY_API_PATHS,
  orderApiPath,
  orderCancellationApiPath
} from './api.paths';

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);

  create(request: CreateOrderRequest): Observable<OrderResponse> {
    return this.http.post<OrderResponse>(GATEWAY_API_PATHS.orders, request);
  }

  list(query: OrderListQuery = {}): Observable<OrderPage> {
    return this.http.get<OrderPage>(GATEWAY_API_PATHS.orders, {
      params: toOrderParams(query)
    });
  }

  get(orderId: string): Observable<OrderResponse> {
    return this.http.get<OrderResponse>(orderApiPath(orderId));
  }

  cancel(orderId: string): Observable<OrderResponse> {
    return this.http.post<OrderResponse>(
      orderCancellationApiPath(orderId),
      null
    );
  }
}

function toOrderParams(query: OrderListQuery): HttpParams {
  let params = new HttpParams();
  if (query.page !== undefined) {
    params = params.set('page', query.page);
  }
  if (query.limit !== undefined) {
    params = params.set('limit', query.limit);
  }
  if (query.status !== undefined) {
    params = params.set('status', query.status);
  }
  if (query.customerEmail !== undefined && query.customerEmail.length > 0) {
    params = params.set('customerEmail', query.customerEmail);
  }
  return params;
}
