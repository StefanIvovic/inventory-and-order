import { Routes } from '@angular/router';
import { Home } from './home/home';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'tenants', loadComponent: () => import('./tenants/tenants-page').then((page) => page.TenantsPage) },
  { path: 'users', loadComponent: () => import('./users/users-page').then((page) => page.UsersPage) },
  { path: 'roles', loadComponent: () => import('./roles/roles-page').then((page) => page.RolesPage) },
  { path: '**', component: Home },
];
