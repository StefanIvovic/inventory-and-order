import { Routes } from '@angular/router';
import { Account } from './account/account';
import { Home } from './home/home';
import { shopMatcher } from './shop';
import { ShopNotFound } from './shop-not-found/shop-not-found';

export const routes: Routes = [
  {
    matcher: shopMatcher,
    children: [
      { path: '', component: Home },
      { path: 'account', component: Account },
      { path: '**', component: Home },
    ],
  },
  { path: '**', component: ShopNotFound },
];
