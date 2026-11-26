import { Page, expect, test } from '@playwright/test';

const adminEmail = process.env['DEMO_ADMIN_EMAIL'] ?? '';
const adminPassword = process.env['DEMO_ADMIN_PASSWORD'] ?? '';

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

async function openUsers(page: Page, search: string): Promise<void> {
  await page.getByRole('link', { name: 'Users', exact: true }).click();
  await expect(page).toHaveURL(/\/backoffice\/users$/);
  await page.getByLabel('Search email').fill(search);
  await expect(page.locator('tr[mat-row]')).toHaveCount(1);
}

test('an administrator adds a staff user, gives a role, and disables them', async ({ page }) => {
  expect(adminEmail, 'DEMO_ADMIN_EMAIL must be set').not.toBe('');
  const clerkEmail = `clerk-${Date.now()}@demo.test`;
  const clerkPassword = 'Clerk#Pass1';

  await signIn(page, adminEmail, adminPassword);
  await page.getByRole('link', { name: 'Users', exact: true }).click();
  await page.getByRole('button', { name: 'Add staff user' }).click();
  const add = page.locator('mat-dialog-container');
  await add.getByLabel('Email').fill(clerkEmail);
  await add.getByLabel('First password').fill(clerkPassword);
  await add.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(add).toHaveCount(0);
  await expect(page.locator('mat-snack-bar-container')).toContainText(`Staff user ${clerkEmail} added.`);

  await openUsers(page, clerkEmail);
  const row = page.locator('tr[mat-row]');
  await expect(row).toContainText('No roles');
  await expect(row).toContainText('Active');
  await row.getByRole('button', { name: 'Roles' }).click();
  const roles = page.locator('mat-dialog-container');
  await expect(roles).toContainText(`Roles for ${clerkEmail}`);
  await roles.getByRole('switch', { name: 'Viewer' }).click();
  await expect(roles.getByRole('switch', { name: 'Viewer' })).toBeChecked();
  await expect(roles.getByRole('switch', { name: 'Viewer' })).toBeEnabled();
  await roles.getByRole('button', { name: 'Close' }).click();
  await expect(row).toContainText('Viewer');

  await signOut(page);
  await signIn(page, clerkEmail, clerkPassword);
  await expect(page.locator('mat-nav-list a')).toHaveText(['Home', 'Users', 'Roles']);
  await openUsers(page, clerkEmail);
  await expect(page.getByRole('button', { name: 'Add staff user' })).toHaveCount(0);
  await expect(page.locator('tr[mat-row]').getByRole('button')).toHaveCount(0);
  await page.getByRole('link', { name: 'Roles', exact: true }).click();
  await expect(page.locator('section h2')).toHaveText(['Administrator', 'Viewer']);

  await signOut(page);
  await signIn(page, adminEmail, adminPassword);
  await openUsers(page, clerkEmail);
  await page.locator('tr[mat-row]').getByRole('button', { name: 'Disable' }).click();
  const confirm = page.locator('mat-dialog-container');
  await expect(confirm).toContainText(`Disable ${clerkEmail}? They will lose access to the backoffice.`);
  await confirm.getByRole('button', { name: 'Disable' }).click();
  await expect(page.locator('mat-snack-bar-container')).toContainText(`${clerkEmail} disabled.`);
  await expect(page.locator('tr[mat-row]')).toContainText('Disabled');

  await signOut(page);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.getByLabel('Email').fill(clerkEmail);
  await page.getByLabel('Password').fill(clerkPassword);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page).toHaveURL(/\/account\/login/);
});
