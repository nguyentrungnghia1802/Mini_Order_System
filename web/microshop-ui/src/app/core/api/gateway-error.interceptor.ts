import {
  HttpErrorResponse,
  HttpInterceptorFn
} from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { toGatewayApiError } from './gateway-error';

export const gatewayErrorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) =>
      error instanceof HttpErrorResponse
        ? throwError(() => toGatewayApiError(error))
        : throwError(() => error)
    )
  );
