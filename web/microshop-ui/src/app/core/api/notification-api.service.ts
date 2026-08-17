import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import {
  NotificationListQuery,
  NotificationPage,
  NotificationResponse
} from './api.models';
import {
  GATEWAY_API_PATHS,
  notificationReadApiPath
} from './api.paths';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly http = inject(HttpClient);

  list(query: NotificationListQuery = {}): Observable<NotificationPage> {
    return this.http.get<NotificationPage>(GATEWAY_API_PATHS.notifications, {
      params: toNotificationParams(query)
    });
  }

  markAsRead(notificationId: string): Observable<NotificationResponse> {
    return this.http.post<NotificationResponse>(
      notificationReadApiPath(notificationId),
      null
    );
  }
}

function toNotificationParams(query: NotificationListQuery): HttpParams {
  let params = new HttpParams();
  if (query.page !== undefined) {
    params = params.set('page', query.page);
  }
  if (query.limit !== undefined) {
    params = params.set('limit', query.limit);
  }
  if (query.customerEmail !== undefined && query.customerEmail.length > 0) {
    params = params.set('customerEmail', query.customerEmail);
  }
  if (query.orderId !== undefined && query.orderId.length > 0) {
    params = params.set('orderId', query.orderId);
  }
  return params;
}
