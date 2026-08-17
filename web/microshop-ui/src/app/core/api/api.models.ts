export interface ProductResponse {
  id: string;
  name: string;
  description: string | null;
  unitPrice: number;
  currency: string;
  availableStock: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
}

export interface ProductPage {
  items: ProductResponse[];
  page: number;
  limit: number;
  total: number;
  totalPages: number;
}

export interface ProductListQuery {
  page?: number;
  limit?: number;
  includeInactive?: boolean;
  search?: string;
}

export interface CreateProductRequest {
  name: string;
  description: string;
  unitPrice: number;
  currency: string;
  initialStock: number;
  isActive: boolean;
}

export interface UpdateProductRequest {
  name?: string;
  description?: string;
  unitPrice?: number;
  availableStock?: number;
  isActive?: boolean;
}

export interface OrderItemRequest {
  productId: string;
  quantity: number;
}

export interface CreateOrderRequest {
  customerName: string;
  customerEmail: string;
  items: OrderItemRequest[];
}

export type OrderStatus =
  | 'pending_inventory'
  | 'confirmed'
  | 'rejected'
  | 'inventory_unknown'
  | 'cancellation_pending'
  | 'cancelled';

export interface OrderItemResponse {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  subtotal: number;
}

export interface OrderResponse {
  id: string;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  currency: string;
  totalAmount: number;
  items: OrderItemResponse[];
  canCancel: boolean;
  failureCode: string | null;
  failureDetail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  confirmedAtUtc: string | null;
  cancelledAtUtc: string | null;
  version: number;
}

export interface OrderPage {
  items: OrderResponse[];
  page: number;
  limit: number;
  total: number;
  totalPages: number;
}

export interface OrderListQuery {
  page?: number;
  limit?: number;
  status?: OrderStatus;
  customerEmail?: string;
}
