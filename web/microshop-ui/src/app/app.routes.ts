import { Routes } from '@angular/router';

import { ProductCatalogComponent } from './features/products/product-catalog.component';
import { ProductManagementComponent } from './features/products/product-management.component';

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
    path: '',
    pathMatch: 'full',
    redirectTo: 'products'
  },
  {
    path: '**',
    redirectTo: 'products'
  }
];
