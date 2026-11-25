import { expect, test } from '@playwright/test';

const slug = process.env['DEMO_TENANT_SLUG'] ?? '';
const shopHome = new RegExp(`/shop/${slug}/?$`);

test('a new shopper registers in the storefront, sees the account and signs out', async ({ page }) => {
  expect(slug, 'DEMO_TENANT_SLUG must be set').not.toBe('');
  const email = `e2e-${Date.now()}@mail.test`;
  const password = 'Shopper#Pass1';

  await page.goto(`/shop/${slug}/`);
  await expect(page.locator('mat-toolbar')).toContainText(slug);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(/\/account\/login/);
  await page.getByRole('link', { name: 'Create an account' }).click();
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page).toHaveURL(shopHome);
  const accountMenu = page.getByRole('button', { name: 'Account menu' });
  await expect(accountMenu).toContainText(email);

  await accountMenu.click();
  await page.getByRole('menuitem', { name: 'Account' }).click();
  await expect(page).toHaveURL(new RegExp(`/shop/${slug}/account$`));
  await expect(page.getByRole('heading', { name: 'Your account' })).toBeVisible();
  await expect(page.locator('main')).toContainText(`Email: ${email}`);
  await expect(page.locator('main')).toContainText(`Shop: ${slug}`);

  await page.goto('/shop/other-shop/');
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Account menu' })).toHaveCount(0);

  await page.goto(`/shop/${slug}/`);
  await page.getByRole('button', { name: 'Account menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await page.getByRole('button', { name: 'Sign out' }).click();

  await expect(page).toHaveURL(shopHome);
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
});
