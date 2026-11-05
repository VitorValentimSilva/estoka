// Gera os prints do README em docs/screenshots (1440px e 390px, tema claro e escuro).
// Uso: BASE_URL=http://localhost:8080 ORDER_ID=<guid de um pedido> node tests/e2e/screenshots.cjs
const { chromium } = require('playwright');
const path = require('node:path');

const base = process.env.BASE_URL ?? 'http://localhost:8080';
const out = path.resolve(__dirname, '../../docs/screenshots');
const orderId = process.env.ORDER_ID;

(async () => {
  const browser = await chromium.launch(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {});
  const shots = [
    { width: 1440, height: 900, suffix: 'desktop' },
    { width: 390, height: 844, suffix: 'mobile' },
  ];
  for (const s of shots) {
    for (const theme of ['light', 'dark']) {
      const ctx = await browser.newContext({ viewport: { width: s.width, height: s.height }, deviceScaleFactor: 2 });
      await ctx.addCookies([{ name: 'estoka-theme', value: theme, url: base }]);
      const page = await ctx.newPage();
      const snap = async (name) => {
        await page.waitForTimeout(1200);
        await page.screenshot({ path: `${out}/${name}-${s.suffix}${theme === 'dark' ? '-escuro' : ''}.png` });
        console.log(`✓ ${name}-${s.suffix} (${theme})`);
      };

      await page.goto(`${base}/login`);
      if (theme === 'light') await snap('login');
      await page.click('[data-demo-email="admin@estoka.app"]');
      await Promise.all([page.waitForURL(`${base}/`), page.click('button[type=submit]')]);
      await page.locator('.kpi').first().waitFor();
      await snap('dashboard');

      if (theme === 'light') {
        await page.goto(`${base}/produtos`);
        await page.locator('.mud-table-body tr').nth(5).waitFor();
        await snap('produtos');
        if (orderId) {
          await page.goto(`${base}/pedidos/${orderId}`);
          await page.getByText('Histórico').waitFor();
          await snap('pedido');
        }
        await page.goto(`${base}/relatorios?aba=1`);
        await page.locator('.abc-summary').waitFor();
        await snap('relatorio-curva-abc');
        if (s.suffix === 'desktop') {
          await page.goto(`${base}/estoque`);
          await page.locator('.mud-table-body tr').nth(5).waitFor();
          await snap('estoque');
          await page.goto(`${base}/auditoria`);
          await page.locator('.mud-table-body tr button').first().click();
          await snap('auditoria');
          await page.goto(`${base}/scalar/`);
          await page.waitForTimeout(2500);
          await snap('api-scalar');
        }
      }
      await ctx.close();
    }
  }
  await browser.close();
})().catch(err => { console.error(err); process.exit(1); });
