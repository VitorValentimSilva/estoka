// E2E do leitor de código de barras: Vendedor monta um pedido bipando (leitor USB = digitação + Enter)
// e lendo pela câmera (vídeo .y4m com EAN-13 como câmera falsa do Chrome); confirma e confere os itens.
// Uso: BASE_URL=http://localhost:8080 node tests/e2e/codigo-de-barras.cjs
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const os = require('node:os');
const path = require('node:path');
const { demoBarcode, writeY4m } = require('./ean13-video.cjs');

const base = process.env.BASE_URL ?? 'http://localhost:8080';
const USB = { code: demoBarcode(2, 2), name: 'Feijão Carioca' };   // MER-002
const CAMERA = { code: demoBarcode(2, 3), name: 'Feijão Preto' };  // MER-003
const step = (m) => console.log(`• ${m}`);

(async () => {
  const video = path.join(os.tmpdir(), `estoka-ean-${process.pid}.y4m`);
  writeY4m(video, CAMERA.code);

  const browser = await chromium.launch({
    ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}),
    args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream', `--use-file-for-fake-video-capture=${video}`],
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  await context.grantPermissions(['camera'], { origin: base });
  const page = await context.newPage();
  globalThis.__page = page;
  page.on('pageerror', e => { throw e; });

  step('login como vendedor');
  await page.goto(`${base}/login`);
  await page.click('[data-demo-email="vendedor@estoka.app"]');
  await Promise.all([page.waitForURL(`${base}/`), page.click('button[type=submit]')]);

  step('novo pedido');
  await page.goto(`${base}/pedidos/novo`);
  await page.getByLabel('Cliente').fill('Mercadinho');
  await page.getByText('Mercadinho São José', { exact: false }).first().click();
  await page.locator('.mud-popover-open').waitFor({ state: 'detached' }).catch(() => {});

  step(`bipa ${USB.code} (leitor USB: digita e envia Enter)`);
  const field = page.getByLabel('Bipar produto');
  await field.pressSequentially(USB.code, { delay: 5 });
  await field.press('Enter');
  await page.locator('.order-items tr', { hasText: USB.name }).waitFor();
  assert.equal(await field.inputValue(), '', 'campo deve limpar após a leitura');

  step('bipa o mesmo código de novo (soma quantidade)');
  await field.pressSequentially(USB.code, { delay: 5 });
  await field.press('Enter');
  await page.waitForFunction((name) => {
    const row = [...document.querySelectorAll('.order-items tr')].find(r => r.textContent.includes(name));
    return row && row.querySelector('input')?.value === '2';
  }, USB.name);

  step(`lê ${CAMERA.code} pela câmera`);
  await page.getByRole('button', { name: 'Ler com a câmera' }).click();
  await page.locator('.order-items tr', { hasText: CAMERA.name }).waitFor({ timeout: 20000 });
  await page.locator('.scanner').waitFor({ state: 'detached' });

  step('confirma o pedido');
  await page.getByRole('button', { name: 'Confirmar pedido' }).click();
  await page.getByText('Confirmado — estoque baixado').waitFor();
  const text = await page.locator('.order-items').innerText();
  assert.match(text, new RegExp(USB.name));
  assert.match(text, new RegExp(CAMERA.name));

  console.log('✔ leitor de código de barras (USB e câmera) concluído com sucesso');
  await browser.close();
  globalThis.__page = null;
})().catch(async err => {
  console.error('✘ falhou:', err.message);
  if (process.env.E2E_SHOT && globalThis.__page) await globalThis.__page.screenshot({ path: process.env.E2E_SHOT }).catch(() => {});
  process.exit(1);
});
