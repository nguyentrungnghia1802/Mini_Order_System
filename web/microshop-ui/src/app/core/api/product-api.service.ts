import {
  HttpClient,
  HttpHeaders,
  HttpParams
} from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CreateProductRequest,
  ProductListQuery,
  ProductPage,
  ProductResponse,
  UpdateProductRequest
} from './api.models';
import { GATEWAY_API_PATHS, productApiPath } from './api.paths';

@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private readonly http = inject(HttpClient);

  list(query: ProductListQuery = {}): Observable<ProductPage> {
    return this.http.get<ProductPage>(GATEWAY_API_PATHS.products, {
      params: toProductParams(query)
    });
  }

  get(productId: string): Observable<ProductResponse> {
    return this.http.get<ProductResponse>(productApiPath(productId));
  }

  create(request: CreateProductRequest): Observable<ProductResponse> {
    return this.http.post<ProductResponse>(GATEWAY_API_PATHS.products, request);
  }

  update(
    productId: string,
    request: UpdateProductRequest,
    version: number
  ): Observable<ProductResponse> {
    return this.http.patch<ProductResponse>(
      productApiPath(productId),
      request,
      {
        headers: new HttpHeaders({
          'If-Match': `"${version}"`
        })
      }
    );
  }
}

function toProductParams(query: ProductListQuery): HttpParams {
  let params = new HttpParams();
  if (query.page !== undefined) {
    params = params.set('page', query.page);
  }
  if (query.limit !== undefined) {
    params = params.set('limit', query.limit);
  }
  if (query.includeInactive !== undefined) {
    params = params.set('includeInactive', query.includeInactive);
  }
  if (query.search !== undefined && query.search.length > 0) {
    params = params.set('search', query.search);
  }
  return params;
}
