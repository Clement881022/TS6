const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '../..');
const output = path.join(__dirname, 'exports');
const mime = { '.html':'text/html; charset=utf-8', '.css':'text/css; charset=utf-8', '.js':'text/javascript; charset=utf-8', '.png':'image/png', '.ttf':'font/ttf' };
const server = http.createServer((req,res)=>{
  try {
    const pathname = decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const file = path.resolve(root,'.'+pathname);
    if(!file.startsWith(root+path.sep) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {res.writeHead(404);res.end();return;}
    res.writeHead(200,{'Content-Type':mime[path.extname(file)] || 'application/octet-stream'});
    fs.createReadStream(file).pipe(res);
  } catch { res.writeHead(400);res.end(); }
});
const executablePath = process.env.UI_REVIEW_BROWSER || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const shots = [
  ['home-A-16x9.png','variant=A&ratio=16:9',800,450],
  ['home-B-16x9.png','variant=B&ratio=16:9',800,450],
  ['home-A-20x9.png','variant=A&ratio=20:9',1000,450],
  ['home-B-20x9.png','variant=B&ratio=20:9',1000,450],
  ['components-A.png','export=components&variant=A',960,560],
  ['components-B.png','export=components&variant=B',960,560],
  ['battle-mobile-16x9.png','page=battle&ratio=16:9',800,450],
  ['battle-mobile-20x9.png','page=battle&ratio=20:9',1000,450],
  ['battle-card-detail.png','page=battle&detail=card',800,450],
  ['battle-target-mode.png','page=battle&detail=target',800,450]
];
function assert(condition,message) {if(!condition) throw new Error(message);}
async function ready(page,url) {
  await page.goto(url);
  await page.evaluate(async()=>{await document.fonts.ready;await Promise.all(Array.from(document.images).map(img=>img.decode().catch(()=>{})));});
  assert(await page.evaluate(()=>Array.from(document.images).every(img=>img.complete && img.naturalWidth > 0)),'Image asset failed to load');
}
async function inspect(page) {
  return page.evaluate(()=>{
    const scene = document.querySelector('.scene'), sr = scene.getBoundingClientRect();
    const all = Array.from(scene.querySelectorAll('button')).filter(e=>!e.disabled && e.getBoundingClientRect().width > 0 && getComputedStyle(e).visibility!=='hidden');
    const measured = all.map(e=>{
      const r=e.getBoundingClientRect();let left=r.left,right=r.right,top=r.top,bottom=r.bottom;
      for(let ancestor=e.parentElement;ancestor && ancestor!==scene;ancestor=ancestor.parentElement) {
        const style=getComputedStyle(ancestor),ar=ancestor.getBoundingClientRect();
        if(['auto','scroll','hidden','clip'].includes(style.overflowX)){left=Math.max(left,ar.left);right=Math.min(right,ar.right);}
        if(['auto','scroll','hidden','clip'].includes(style.overflowY)){top=Math.max(top,ar.top);bottom=Math.min(bottom,ar.bottom);}
      }
      return {element:e,bound:{name:e.getAttribute('aria-label')||e.textContent.trim(),minimum:e.closest('.board-zone')?44:48,width:Math.max(0,right-left),height:Math.max(0,bottom-top),x:left-sr.left,y:top-sr.top,inside:left>=sr.left && top>=sr.top && right<=sr.right+.5 && bottom<=sr.bottom+.5}};
    }).filter(m=>m.bound.width>1 && m.bound.height>1);
    const active=measured.map(m=>m.element),bounds=measured.map(m=>m.bound);
    const overlay = scene.querySelector('.battle-detail:not(.hidden),.battle-drawer:not(.hidden)');
    const checked = overlay && !overlay.classList.contains('card-detail') ? bounds.filter((b,i)=>overlay.contains(active[i]) || !active[i].closest('.board-zone')) : bounds;
    const interactions = checked.map((b,i)=>({...b}));
    const overlaps=[];
    if(!overlay || overlay.classList.contains('card-detail')) for(let i=0;i<interactions.length;i++) for(let j=i+1;j<interactions.length;j++) {
      const a=interactions[i],b=interactions[j];
      if(Math.min(a.x+a.width,b.x+b.width)-Math.max(a.x,b.x)>1 && Math.min(a.y+a.height,b.y+b.height)-Math.max(a.y,b.y)>1) overlaps.push([a.name,b.name]);
    }
    return {width:sr.width,height:sr.height,targets:bounds,minimumTouchTarget:48,minimumBoardTarget:44,invalidTargets:checked.filter(b=>b.width<b.minimum-.5 || b.height<b.minimum-.5 || !b.inside),overlaps,loadedImages:Array.from(document.images).length,documentFontStatus:document.fonts.status};
  });
}
async function run() {
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const base = `http://127.0.0.1:${server.address().port}/docs/ui-commercial-review/index.html`;
  if(process.argv.includes('--serve')) {console.log(base);return;}
  fs.mkdirSync(output,{recursive:true});
  const browser = await chromium.launch({executablePath,headless:true});
  const report={generatedAt:new Date().toISOString(),scope:'HTML/CSS design prototype, not Unity or physical phone acceptance',screenshots:[],interactionChecks:[],errors:[]};
  try {
    for(const [name,query,width,height] of shots) {
      const page = await browser.newPage({viewport:{width,height},deviceScaleFactor:2});
      page.on('pageerror',e=>report.errors.push(e.message));
      page.on('console',m=>{if(m.type()==='error')report.errors.push(m.text());});
      const exportQuery = query.includes('export=') ? query : `export=scene&${query}`;
      await ready(page,`${base}?${exportQuery}`);
      const locator = page.locator(query.includes('export=components') ? '.component-sheet' : '.scene');
      await locator.screenshot({path:path.join(output,name),animations:'disabled'});
      const renderedBox = await locator.boundingBox();
      const geometry=query.includes('export=components') ? null : await inspect(page);
      if(geometry) {
        assert(geometry.invalidTargets.length===0,`${name}: invalid touch targets ${JSON.stringify(geometry.invalidTargets)}`);
        assert(geometry.overlaps.length===0,`${name}: overlapping targets ${JSON.stringify(geometry.overlaps)}`);
      }
      report.screenshots.push({file:name,pixels:[Math.round(renderedBox.width*2),Math.round(renderedBox.height*2)],logical:[renderedBox.width,renderedBox.height],geometry});
      await page.close();
    }
    const page=await browser.newPage({viewport:{width:1100,height:900}});
    page.on('pageerror',e=>report.errors.push(e.message));
    await ready(page,base);
    await page.locator('[data-variant="B"]').click();
    assert(await page.locator('.scene').evaluate(e=>e.classList.contains('variant-b')),'Variant switch failed');
    await page.locator('[data-ratio="20:9"]').click();
    assert(await page.locator('.scene').evaluate(e=>e.classList.contains('wide')),'Ratio switch failed');
    await page.locator('#safe-toggle').check();
    assert(await page.locator('.safe-overlay').isVisible(),'Safe-area overlay failed');
    await page.locator('#viewport [data-nav="養成"]').click();
    assert(await page.locator('.demo-status').textContent().then(t=>t.includes('養成')),'Navigation preview feedback failed');
    await page.locator('#viewport .primary').click();
    assert(await page.locator('dialog').isVisible(),'Home primary feedback failed');
    await page.locator('.dialog-done').click();
    report.interactionChecks.push('home: variant, aspect ratio, safe area, navigation feedback, primary feedback');
    await page.locator('[data-page="battle"]').click();
    await page.locator('[data-ratio="16:9"]').click();
    await page.locator('[data-card="0"]').click();
    assert(await page.locator('.battle-detail').isVisible(),'Card detail did not open on click');
    assert(await page.locator('.target-option').count()===4,'Ally target preview mismatch');
    await page.locator('[data-cell="2"]').click();
    assert(await page.locator('.demo-status').textContent().then(t=>t.includes('友軍')),'Wrong-side target feedback failed');
    await page.locator('[data-cell="16"]').click();
    assert(await page.locator('dialog').isVisible(),'Target confirmation preview failed');
    await page.locator('.dialog-done').click();
    await page.locator('[data-card="1"]').click();
    assert(await page.locator('.target-option').count()===3,'Enemy target preview mismatch');
    await page.locator('[data-cell="2"]').click();
    await page.locator('.dialog-done').click();
    await page.locator('[data-squad="0"]').click();
    assert(await page.locator('.battle-detail').textContent().then(t=>t.includes('530 / 884')),'Squad detail mismatch');
    await page.locator('.detail-close').click();
    await page.locator('.next').click();
    await page.waitForFunction(()=>document.querySelector('.hand-viewport').scrollLeft>20);
    await page.locator('[data-card="6"]').click();
    assert(await page.locator('.battle-detail').textContent().then(t=>t.includes('移動')),'Last card unreachable');
    await page.locator('.detail-close').click();
    await page.locator('.battle-menu').click();
    assert(await page.locator('.battle-drawer').isVisible(),'Battle menu failed');
    assert(await page.locator('.battle-drawer').textContent().then(t=>!t.includes('直接勝利') || t.includes('不列入')),'Debug action leaked into player actions');
    await page.locator('[data-menu-action="撤退"]').click();
    assert(await page.locator('dialog').isVisible(),'Retreat preview did not show confirmation');
    await page.locator('.dialog-done').click();
    await page.locator('.battle-menu').click();
    await page.locator('.turn-end').click();
    assert(await page.locator('dialog').isVisible(),'Turn-end feedback failed');
    await page.locator('.dialog-done').click();
    report.interactionChecks.push('battle: two-tap card-to-target, ally/enemy target preview, wrong-side feedback, target confirmation, unit detail, hand scroll, last card, menu, retreat and end-turn feedback');
    for(const width of [800,1000]) {
      const touch=await browser.newPage({viewport:{width,height:450},deviceScaleFactor:2,hasTouch:true,isMobile:true});
      await ready(touch,`${base}?export=scene&page=battle&ratio=${width===800?'16:9':'20:9'}`);
      await touch.locator('[data-card="0"]').tap();
      assert(await touch.locator('.battle-detail').isVisible(),'Touch card detail failed');
      await touch.locator('[data-cell="16"]').tap();
      assert(await touch.locator('dialog').isVisible(),'Touch target confirmation failed');
      report.interactionChecks.push(`touch emulation ${width}x450: tap card -> tap ally`);
      await touch.close();
    }
    assert(report.errors.length===0,`Browser errors: ${report.errors.join('; ')}`);
    fs.writeFileSync(path.join(output,'verification.json'),JSON.stringify(report,null,2));
    console.log(JSON.stringify({screenshots:report.screenshots.length,interactionChecks:report.interactionChecks,errors:report.errors},null,2));
    await page.close();
  } finally {await browser.close();server.close();}
}
run().catch(e=>{console.error(e);server.close();process.exitCode=1;});
