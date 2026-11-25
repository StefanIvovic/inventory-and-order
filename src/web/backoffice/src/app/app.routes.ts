import { Routes } from '@angular/router';
import { Home } from './home/home';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'tenants', loadComponent: () => import('./tenants/tenants-page').then((page) => page.TenantsPage) },
  { path: '**', component: Home },
];
