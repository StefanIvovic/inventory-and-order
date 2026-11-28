import { expect, test } from '@playwright/test';

const adminEmail = process.env['DEMO_ADMIN_EMAIL'] ?? '';
const adminPassword = process.env['DEMO_ADMIN_PASSWORD'] ?? '';

test('a signed-in staff user reaches the Product service, which checks its own permissions', async ({ page }) => {
  expect(adminEmail, 'DEMO_ADMIN_EMAIL must be set').not.toBe('');

  const anonymous = await page.request.get('/api/product/roles', { headers: { 'X-Bff-App': 'backoffice' } });
  expect(anonymous.status()).toBe(401);

  await page.goto('/backoffice/');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/account\/login/);
  await page.getByLabel('Email').fill(adminEmail);
  await page.getByLabel('Password').fill(adminPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.locator('mat-toolbar')).toContainText(adminEmail);

  const response = await page.request.get('/api/product/roles', { headers: { 'X-Bff-App': 'backoffice' } });

  expect(response.status()).toBe(403);
  const problem = await response.json();
  expect(problem.missingPermissions).toEqual(['product.roles.view']);
});
