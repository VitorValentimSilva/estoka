// Fluxo ponta a ponta: Vendedor faz login → cria pedido → confirma → confere a baixa de estoque.
// Uso: BASE_URL=http://localhost:8080 node tests/e2e/pedido-vendedor.cjs [pasta-de-prints]
// Requer o pacote "playwright" (npm i -D playwright) e o app rodando com Demo:Enabled=true.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');

const base = process.env.BASE_URL ?? 'http://localhost:8080';
const shots = process.argv[2];
const SKU = 'MER-001'; // Arroz Tipo 1 5kg
const QTY = 3;

(async () => {
const browser = await chromium.launch(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {});
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
page.on('pageerror', e => { throw e; });
const step = (msg) => console.log(`• ${msg}`);

async function stockOf(sku) {
  await page.goto(`${base}/produtos`);
  await page.getByPlaceholder('Buscar por nome ou SKU').fill(sku);
  const row = page.locator('tr', { hasText: sku }).first();
  await row.waitFor();
  await page.waitForTimeout(600);
  const cells = await row.locator('td').allInnerTexts();
  const stockCell = cells.find(t => /\d+\s*(UN|PCT|CX|FD|KG|L|GF|GL)/.test(t));
  return Number(stockCell.replace(/[^\d,]/g, '').replace(',', '.'));
}

step('login como vendedor');
await page.goto(`${base}/login`);
await page.click('[data-demo-email="vendedor@estoka.app"]');
await Promise.all([page.waitForURL(`${base}/`), page.click('button[type=submit]')]);

const before = await stockOf(SKU);
step(`estoque inicial de ${SKU}: ${before}`);

step('cria pedido');
await page.goto(`${base}/pedidos/novo`);
await page.getByLabel('Cliente').fill('Mercadinho');
await page.getByText('Mercadinho São José', { exact: false }).first().click();
await page.getByPlaceholder('Adicionar produto: digite nome ou SKU').fill(SKU);
await page.locator('.mud-popover-open .mud-list-item', { hasText: SKU }).first().click();
const qty = page.getByLabel('Quantidade').first();
await qty.fill(String(QTY));
await qty.press('Tab');
await page.waitForTimeout(300);
if (shots) await page.screenshot({ path: `${shots}/pedido-novo-1440.png` });

step('confirma pedido');
await page.getByRole('button', { name: 'Confirmar pedido' }).click();
await page.waitForURL(/\/pedidos\/[0-9a-f-]{36}$/);
await page.getByText('Confirmado — estoque baixado').waitFor();
const code = (await page.locator('h1').innerText()).trim();
step(`${code} confirmado`);
if (shots) await page.screenshot({ path: `${shots}/pedido-confirmado-1440.png` });

const after = await stockOf(SKU);
step(`estoque final de ${SKU}: ${after}`);
assert.equal(after, before - QTY, `estoque deveria cair ${QTY} (de ${before} para ${before - QTY}), mas ficou ${after}`);

step('movimentação de saída registrada');
await page.goto(`${base}/estoque`);
await page.getByPlaceholder('Buscar produto, SKU ou motivo').fill(code.replace('Pedido ', ''));
await page.locator('tr', { hasText: SKU }).first().waitFor();

console.log('✔ fluxo do vendedor concluído com sucesso');
await browser.close();
})().catch(err => { console.error('✘ falhou:', err.message); process.exit(1); });
