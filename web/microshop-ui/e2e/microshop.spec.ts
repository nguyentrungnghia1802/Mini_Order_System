import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

type Product = {
  id: string;
  name: string;
  availableStock: number;
  version: number;
};

type Order = {
  id: string;
  status: string;
  totalAmount: number;
  currency: string;
};

type NotificationPage = {
  items: Array<{ orderId: string }>;
};

test.describe.configure({ mode: 'serial' });

test('runs catalog, checkout, notification, cancellation, and stock flows', async ({
  page,
  request
}) => {
  const productName = `Playwright Product ${Date.now()}`;

  await page.goto('/products/manage');
  await expect(page.getByRole('heading', { name: 'Manage Products' })).toBeVisible();

  await page.locator('#product-name').fill(productName);
  await page.locator('#product-description').fill('Created and updated by the Compose E2E suite.');
  await page.locator('#product-price').fill('125000');
  await page.locator('#product-stock').fill('2');

  const createResponsePromise = page.waitForResponse((response) =>
    isApiResponse(response, '/api/products', 'POST')
  );
  await page.getByRole('button', { name: 'Create Product', exact: true }).click();
  const createResponse = await createResponsePromise;
  expect(createResponse.ok()).toBeTruthy();
  const createdProduct = (await createResponse.json()) as Product;
  expect(createdProduct.id).toBeTruthy();
  await expect(page.getByText('Product created.', { exact: true })).toBeVisible();

  const record = page.locator('article.record').filter({ hasText: productName });
  await expect(record).toBeVisible();
  await record.getByRole('button', { name: 'Edit' }).click();
  await page.locator('#product-description').fill('Updated by the Compose E2E suite.');

  const updateResponsePromise = page.waitForResponse((response) =>
    isApiResponse(response, `/api/products/${createdProduct.id}`, 'PATCH')
  );
  await page.getByRole('button', { name: 'Save changes', exact: true }).click();
  const updateResponse = await updateResponsePromise;
  expect(updateResponse.ok()).toBeTruthy();
  await expect(page.getByText('Product updated.', { exact: true })).toBeVisible();

  await page.goto('/products');
  await expect(page.getByRole('heading', { name: 'Product catalog' })).toBeVisible();
  await expect(page.locator('article.product-card').filter({ hasText: productName })).toContainText(
    'Updated by the Compose E2E suite.'
  );

  await page.goto('/checkout');
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible();
  await page.locator('#customer-name').fill('Playwright Shopper');
  await page.locator('#customer-email').fill(`playwright-${Date.now()}@example.com`);
  await page.locator(`#quantity-${createdProduct.id}`).fill('1');

  const createOrderResponsePromise = page.waitForResponse((response) =>
    isApiResponse(response, '/api/orders', 'POST')
  );
  await page.getByRole('button', { name: 'Submit order', exact: true }).click();
  const createOrderResponse = await createOrderResponsePromise;
  expect(createOrderResponse.status()).toBe(201);
  const confirmedOrder = (await createOrderResponse.json()) as Order;
  expect(confirmedOrder.status).toBe('confirmed');
  await expect(page.getByText('Order confirmed', { exact: true })).toBeVisible();
  await expect(page.getByText(confirmedOrder.id, { exact: false })).toBeVisible();

  await page.getByRole('link', { name: 'View order detail' }).click();
  await page.waitForURL(`**/orders/${confirmedOrder.id}`);
  await expect(page.getByRole('heading', { name: 'Order detail' })).toBeVisible();
  await expect(page.locator('.order-detail .eyebrow')).toContainText('confirmed');

  await waitForNotification(request, confirmedOrder.id);
  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: 'Notifications' })).toBeVisible();
  await expect(page.locator('.notification-card').filter({ hasText: confirmedOrder.id })).toBeVisible();

  const cancelResponsePromise = page.waitForResponse((response) =>
    isApiResponse(response, `/api/orders/${confirmedOrder.id}/cancel`, 'POST')
  );
  await page.goto(`/orders/${confirmedOrder.id}`);
  await page.getByRole('button', { name: 'Cancel order', exact: true }).click();
  const cancelResponse = await cancelResponsePromise;
  expect(cancelResponse.ok()).toBeTruthy();
  await expect(page.locator('.order-detail .eyebrow')).toContainText('cancelled');
  await expect(page.getByRole('button', { name: 'Cancel order', exact: true })).toHaveCount(0);

  const restoredProduct = await readProduct(request, createdProduct.id);
  expect(restoredProduct.availableStock).toBe(2);

  await page.goto('/checkout');
  await page.locator('#customer-name').fill('Playwright Stock Check');
  await page.locator('#customer-email').fill(`playwright-stock-${Date.now()}@example.com`);
  await page.locator(`#quantity-${createdProduct.id}`).fill('3');
  const rejectedResponsePromise = page.waitForResponse((response) =>
    isApiResponse(response, '/api/orders', 'POST')
  );
  await page.getByRole('button', { name: 'Submit order', exact: true }).click();
  const rejectedResponse = await rejectedResponsePromise;
  expect(rejectedResponse.status()).toBe(409);
  await expect(page.getByText('Order rejected', { exact: true })).toBeVisible();
  expect((await readProduct(request, createdProduct.id)).availableStock).toBe(2);
});

test('renders the dependency failure outcome without confirming an order', async ({ page }) => {
  await page.route('**/api/orders', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }

    await route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        code: 'PRODUCT_SERVICE_UNAVAILABLE',
        detail: 'Product Service is offline for this browser failure test.'
      })
    });
  });

  await page.goto('/checkout');
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible();
  await page.locator('#customer-name').fill('Playwright Dependency Check');
  await page.locator('#customer-email').fill(`playwright-dependency-${Date.now()}@example.com`);
  await page.locator('input[id^="quantity-"]').first().fill('1');
  await page.getByRole('button', { name: 'Submit order', exact: true }).click();

  await expect(page.getByText('Order status is unknown', { exact: true })).toBeVisible();
  await expect(page.getByRole('alert')).toContainText('Product Service is offline');
});

function isApiResponse(response: { url(): string; request(): { method(): string } }, path: string, method: string): boolean {
  return response.request().method() === method && new URL(response.url()).pathname === path;
}

async function readProduct(request: APIRequestContext, productId: string): Promise<Product> {
  const response = await request.get(`/api/products/${productId}`);
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as Product;
}

async function waitForNotification(request: APIRequestContext, orderId: string): Promise<void> {
  await expect
    .poll(
      async () => {
        const response = await request.get(
          `/api/notifications?orderId=${encodeURIComponent(orderId)}&page=1&limit=20`
        );
        if (!response.ok()) {
          return false;
        }

        const page = (await response.json()) as NotificationPage;
        return page.items.some((notification) => notification.orderId === orderId);
      },
      { timeout: 30_000, intervals: [500, 1_000, 2_000, 4_000, 8_000] }
    )
    .toBe(true);
}
