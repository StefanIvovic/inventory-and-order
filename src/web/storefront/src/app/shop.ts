import { UrlMatcher } from '@angular/router';

export const SHOP_SLUG_PATTERN = /^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$/;

const shopPrefix = '/shop/';

export function isShopSlug(value: string): boolean {
  return SHOP_SLUG_PATTERN.test(value);
}

export function shopSlugFromPath(path: string): string | null {
  if (!path.startsWith(shopPrefix)) {
    return null;
  }

  const segment = path.slice(shopPrefix.length).split('/')[0] ?? '';
  return isShopSlug(segment) ? segment : null;
}

export const shopMatcher: UrlMatcher = (segments) => {
  const first = segments[0];
  return first && isShopSlug(first.path) ? { consumed: [first], posParams: { slug: first } } : null;
};
