import { Page, expect, test } from '@playwright/test';

const platformEmail = process.env['PLATFORM_ADMIN_EMAIL'] ?? '';
const platformPassword = process.env['PLATFORM_ADMIN_PASSWORD'] ?? '';

async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/backoffice/');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/account\/login/);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.locator('mat-toolbar')).toContainText(email);
}

async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'User menu' }).click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
}

test('the platform admin creates a tenant whose first admin can then sign in', async ({ page }) => {
  expect(platformEmail, 'PLATFORM_ADMIN_EMAIL must be set').not.toBe('');
  const slug = `e2e-${Date.now()}`;
  const adminEmail = `admin@${slug}.test`;
  const adminPassword = 'Tenant#Pass1';

  await signIn(page, platformEmail, platformPassword);
  await page.getByRole('link', { name: 'Tenants', exact: true }).click();
  await expect(page).toHaveURL(/\/backoffice\/tenants$/);

  await page.getByRole('button', { name: 'Create tenant' }).click();
  const dialog = page.locator('mat-dialog-container');
  await expect(dialog.getByLabel('Identifier')).toBeFocused();
  await dialog.getByLabel('Identifier').fill(slug);
  await dialog.getByLabel('Name').fill(`${slug} Ltd`);
  await dialog.getByLabel('First admin email').fill(adminEmail);
  await dialog.getByLabel('First admin password').fill(adminPassword);
  await dialog.getByRole('button', { name: 'Create' }).click();

  await expect(dialog).toHaveCount(0);
  await expect(page.locator('mat-snack-bar-container')).toContainText(`Tenant ${slug} created.`);
  await page.getByRole('searchbox', { name: 'Search tenants' }).fill(slug);
  await page.getByRole('searchbox', { name: 'Search tenants' }).press('Enter');
  await expect(page.locator('tr[mat-row]')).toHaveCount(1);
  await expect(page.locator('tr[mat-row]').first()).toContainText(slug);
  await expect(page.locator('.range')).toHaveText('1–1 of 1 tenants');

  await signOut(page);
  await signIn(page, adminEmail, adminPassword);

  await expect(page.locator('mat-toolbar')).toContainText('Staff');
  await expect(page.locator('mat-nav-list a')).toHaveText(['Home', 'Users', 'Roles']);
  await expect(page.getByRole('link', { name: 'Tenants', exact: true })).toHaveCount(0);
});
