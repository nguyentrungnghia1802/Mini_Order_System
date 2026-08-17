export const GATEWAY_API_PATHS = {
  products: '/api/products',
  orders: '/api/orders',
  notifications: '/api/notifications'
} as const;

export function productApiPath(productId: string): string {
  return `${GATEWAY_API_PATHS.products}/${encodeURIComponent(productId)}`;
}

export function orderApiPath(orderId: string): string {
  return `${GATEWAY_API_PATHS.orders}/${encodeURIComponent(orderId)}`;
}

export function orderCancellationApiPath(orderId: string): string {
  return `${orderApiPath(orderId)}/cancel`;
}

export function notificationReadApiPath(notificationId: string): string {
  return `${GATEWAY_API_PATHS.notifications}/${encodeURIComponent(notificationId)}/read`;
}
