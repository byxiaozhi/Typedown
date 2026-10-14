// The HTML that becomes the PDF must link its contents to the headings the parser actually rendered.
// A scan of source lines used to invent headings inside code fences and miss setext headings.
const { openEditor } = require('./harness');

const markdown = '# Document\n\n[TOC]\n\n## Formatted **heading**\n\n### Closed heading ###\n\nSetext heading\n--------------\n\n```text\n# Not a heading\n```\n```text\nsecond fence\n```\n\n| ID | Name | Category | Region | Q1 | Q2 | Q3 | Q4 | Total | Change | Owner team | Notes |\n|---:|------|----------|--------|---:|---:|---:|---:|------:|:------:|------------|-------|\n| 1 | Widget | Hardware | North | 120 | 135 | 150 | 170 | 575 | +12% | Platform | Steady growth through the year |\n| 2 | Service plan | Software | East | 300 | 310 | 305 | 330 | 1245 | +8% | Cloud | Renewals dominate |\n\n' +
  '| API | Typical resource | Purpose |\n|-----|------------------|---------|\n' +
  '| AEF_Security_API（Initiate_Authentication） | POST {apiRoot}/aef-security/v1/check-authentication | API 调用者请求 AEF 确认或补齐认证参数 |\n' +
  '| CAPIF_Security_API（Obtain_API_Invoker_Info） | GET {apiRoot}/capif-security/v1/trustedInvokers/{apiInvokerId} | AEF 缺少信息时从 CCF 获取调用者认证或授权信息 |\n' +
  '| CAPIF_Logging_API_Invocation_API（Log_API_Invocation） | POST {apiRoot}/api-invocation-logs/v1/{aefId}/logs | AEF 向 CCF 写入服务 API 调用日志 |\n\n' +
  '| API | Typical resource | Purpose |\n|-----|------------------|---------|\n' +
  '| CAPIF_Security_API | /trustedInvokers/{apiInvokerId} | AEF 查询或维护调用者安全信息 |\n' +
  '| CAPIF_Access_Control_Policy_API | /accessControlPolicyList/{serviceApiId} | 获取指定服务 API 的访问控制策略 |\n' +
  '| CAPIF_Logging_API_Invocation_API | /{aefId}/logs | 写入服务 API 调用日志 |\n' +
  '| CAPIF_Routing_Info_API | /service-apis/{serviceApiId} | 获取服务 API 动态路由规则 |\n\n' +
  '| Item | Owner | Status |\n|------|-------|--------|\n| One | Team A | Open |\n| Two | Team B | Done |\n\n' +
  '| Identifier | Resource | Result |\n|------------|----------|--------|\n' +
  '| customer_subscription_configuration | /accounts/{accountId}/subscriptions/{subscriptionId} | Created |\n' +
  '| external_inventory_reconciliation | /inventory/{warehouseId}/reconciliation | Updated |\n';

(async () => {
  const { page, load, close } = await openEditor();
  try {
    await load(markdown);
    await page.evaluate(() => {
      window.__export = null;
      const previous = window.chrome.webview.postMessage;
      window.chrome.webview.postMessage = raw => {
        const message = JSON.parse(raw);
        if (message.type === 'invoke' && message.name === 'ExportCallback') window.__export = message.args;
        previous(raw);
      };
      window.__deliver('Export', { type: 'html', context: 1, basePath: '/tmp', title: 'Document', options: { extraHead: '<style>.markdown-body { min-width: unset; max-width: unset; margin: unset; padding: 0; }</style>' } });
    });
    await page.waitForFunction(() => window.__export, { timeout: 30000 });
    const result = await page.evaluate(() => window.__export);
    if (result.error) throw new Error(result.error);
    const html = typeof result === 'string' ? JSON.parse(result).html : result.html;
    const exported = await page.browser().newPage();
    try {
      // A4 with the application's default 3.18 cm PDF margins leaves about 553 CSS px.
      await exported.setViewport({ width: 553, height: 1000 });
      await exported.setContent(html, { waitUntil: 'load' });
      // A generic theme may reset pre margins; the separator rule must still distinguish fences.
      await exported.addStyleTag({ content: '.markdown-body pre { margin: 0; }' });
      const state = await exported.evaluate(() => {
        const links = [...document.querySelectorAll('.toc-container a')];
        const pres = [...document.querySelectorAll('.markdown-body pre')];
        const table = document.querySelector('.markdown-body table.export-wide-table');
        const threeColumnTables = [...document.querySelectorAll('.markdown-body table')].filter(candidate => candidate.rows[0]?.cells.length === 3);
        const cells = [...table.rows[1].cells];
        const numericLines = [...table.querySelectorAll('td')]
          .filter(cell => /^[+\-−]?\d[\d.,%]*$/u.test(cell.textContent.trim()))
          .map(cell => { const range = document.createRange(); range.selectNodeContents(cell); const canvas = document.createElement('canvas'); const context = canvas.getContext('2d'); context.font = getComputedStyle(cell).font; return { text: cell.textContent.trim(), lines: range.getClientRects().length, cellWidth: cell.getBoundingClientRect().width, textWidth: context.measureText(cell.textContent.trim()).width }; });
        const idHeaderRange = document.createRange();
        idHeaderRange.selectNodeContents(table.rows[0].cells[0]);
        return {
          links: links.map(a => ({ text: a.textContent, target: document.getElementById(a.getAttribute('href').slice(1))?.textContent })),
          codeGap: pres[1].getBoundingClientRect().top - pres[0].getBoundingClientRect().bottom,
          table: { widths: table.getAttribute('data-export-col-widths'), numericLines, idHeaderLines: idHeaderRange.getClientRects().length, notesWidth: cells[11].getBoundingClientRect().width, numericWidth: cells[4].getBoundingClientRect().width, totalWidth: table.getBoundingClientRect().width },
          denseTable: threeColumnTables[0]?.getAttribute('data-export-col-widths')?.split(',').map(Number),
          shortApiTable: threeColumnTables[1]?.getAttribute('data-export-col-widths')?.split(',').map(Number),
          ordinaryTable: threeColumnTables[2]?.getAttribute('data-export-col-widths'),
          genericLongTokenTable: threeColumnTables[3]?.getAttribute('data-export-col-widths')?.split(',').map(Number)
        };
      });
      if (state.links.length !== 3 || state.links.some(x => x.text !== x.target))
        throw new Error(`TOC entries do not resolve to their headings: ${JSON.stringify(state.links)}`);
      if (state.codeGap < 10) throw new Error(`Adjacent code fences have only ${state.codeGap}px between them`);
      if (!state.table.widths || state.table.numericLines.some(item => item.lines !== 1) || state.table.idHeaderLines !== 1 || state.table.notesWidth / state.table.totalWidth < 0.25)
        throw new Error(`Wide table columns remain too narrow: ${JSON.stringify(state.table)}`);
      if (!state.denseTable || state.denseTable[1] <= 3800 || state.denseTable[2] >= 3000)
        throw new Error(`Dense three-column table still has equal widths: ${JSON.stringify(state.denseTable)}`);
      if (!state.shortApiTable || state.shortApiTable[0] <= 3000 || state.shortApiTable[2] >= 3000)
        throw new Error(`Short API table with long identifiers still has equal widths: ${JSON.stringify(state.shortApiTable)}`);
      if (state.ordinaryTable) throw new Error(`Ordinary short table was compacted: ${state.ordinaryTable}`);
      if (!state.genericLongTokenTable || state.genericLongTokenTable[0] <= 3000)
        throw new Error(`Generic long identifiers did not trigger content widths: ${JSON.stringify(state.genericLongTokenTable)}`);
      console.log(`PASS: ${state.links.length} TOC links resolve; adjacent code fences have ${state.codeGap}px gap; wide table numbers stay on one line`);
    } finally { await exported.close(); }
  } finally { await close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
