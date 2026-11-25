import { expect, test } from '@playwright/test';

const email = process.env['DEMO_ADMIN_EMAIL'] ?? '';
const password = process.env['DEMO_ADMIN_PASSWORD'] ?? '';

test('a staff member signs in to the backoffice and signs out', async ({ page }) => {
  expect(email, 'DEMO_ADMIN_EMAIL must be set').not.toBe('');

  await page.goto('/backoffice/orders');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(/\/account\/login/);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(/\/backoffice\/orders$/);
  const toolbar = page.locator('mat-toolbar');
  await expect(toolbar).toContainText(email);
  await expect(toolbar).toContainText('Staff');

  await page.getByRole('button', { name: 'User menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await page.getByRole('button', { name: 'Sign out' }).click();

  await expect(page).toHaveURL(/\/backoffice\/$/);
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
});
