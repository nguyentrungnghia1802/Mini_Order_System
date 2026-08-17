import { HttpErrorResponse } from '@angular/common/http';

export interface GatewayProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
}

export type GatewayErrorKind = 'connectivity' | 'api' | 'unknown';

export class GatewayApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly kind: GatewayErrorKind,
    readonly code?: string,
    readonly problem?: GatewayProblemDetails
  ) {
    super(message);
    this.name = 'GatewayApiError';
  }
}

export function toGatewayApiError(error: HttpErrorResponse): GatewayApiError {
  const problem = isGatewayProblemDetails(error.error) ? error.error : undefined;
  const kind = isConnectivityStatus(error.status) ? 'connectivity' : 'api';
  const message =
    problem?.detail ??
    problem?.title ??
    (kind === 'connectivity'
      ? 'The MicroShop Gateway is unavailable.'
      : error.message);

  return new GatewayApiError(
    message,
    error.status,
    kind,
    problem?.code,
    problem
  );
}

function isConnectivityStatus(status: number): boolean {
  return status === 0 || status === 502 || status === 503 || status === 504;
}

function isGatewayProblemDetails(
  value: unknown
): value is GatewayProblemDetails {
  return typeof value === 'object' && value !== null;
}
