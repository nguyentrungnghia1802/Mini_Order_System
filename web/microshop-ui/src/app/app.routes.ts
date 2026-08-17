import { Routes } from '@angular/router';

import { ProductCatalogComponent } from './features/products/product-catalog.component';
import { ProductManagementComponent } from './features/products/product-management.component';
import { OrderCheckoutComponent } from './features/orders/order-checkout.component';
import { OrderDetailComponent } from './features/orders/order-detail.component';
import { OrderListComponent } from './features/orders/order-list.component';
import { NotificationListComponent } from './features/notifications/notification-list.component';

export const routes: Routes = [
  {
    path: 'products/manage',
    component: ProductManagementComponent
  },
  {
    path: 'products',
    component: ProductCatalogComponent,
    pathMatch: 'full'
  },
  {
    path: 'checkout',
    component: OrderCheckoutComponent
  },
  {
    path: 'orders',
    component: OrderListComponent,
    pathMatch: 'full'
  },
  {
    path: 'orders/:orderId',
    component: OrderDetailComponent
  },
  {
    path: 'notifications',
    component: NotificationListComponent,
    pathMatch: 'full'
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'products'
  },
  {
    path: '**',
    redirectTo: 'products'
  }
];
