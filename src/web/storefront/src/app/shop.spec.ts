import { UrlSegment, UrlSegmentGroup } from '@angular/router';
import { isShopSlug, shopMatcher, shopSlugFromPath } from './shop';

const sixtyThree = 'abcdefghij'.repeat(6) + 'abc';

describe('shop identifier', () => {
  it.each(['acme', 'demo', 'a1b', 'my-shop', 'a-b-c', sixtyThree])('accepts %s', (slug) => {
    expect(isShopSlug(slug)).toBe(true);
  });

  it.each(['ab', 'Acme', '-acme', 'acme-', 'ac--me', 'ac_me', 'ac.me', sixtyThree + 'd', ''])('rejects %s', (slug) => {
    expect(isShopSlug(slug)).toBe(false);
  });

  it.each([
    ['/shop/demo/', 'demo'],
    ['/shop/demo', 'demo'],
    ['/shop/demo/account', 'demo'],
    ['/shop/', null],
    ['/shop', null],
    ['/shop/Demo/', null],
    ['/elsewhere/demo/', null],
  ])('reads the shop from %s', (path, slug) => {
    expect(shopSlugFromPath(path)).toBe(slug);
  });

  it('matches exactly one valid segment and exposes it as slug', () => {
    const segments = [new UrlSegment('demo', {}), new UrlSegment('account', {})];

    const result = shopMatcher(segments, new UrlSegmentGroup(segments, {}), { path: '' });

    expect(result?.consumed.map((segment) => segment.path)).toEqual(['demo']);
    expect(result?.posParams?.['slug']?.path).toBe('demo');
  });

  it.each([[[]], [[new UrlSegment('Demo', {})]]])('does not match an absent or invalid segment', (segments) => {
    expect(shopMatcher(segments, new UrlSegmentGroup(segments, {}), { path: '' })).toBeNull();
  });
});
