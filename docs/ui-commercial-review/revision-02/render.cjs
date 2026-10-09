const fs=require('node:fs');
const path=require('node:path');
const http=require('node:http');
const {chromium}=require('playwright');
const sharp=require('sharp');
const root=path.resolve(__dirname,'../../..');
const output=path.join(__dirname,'exports');
const assetFolder=path.join(root,'client/Assets/Resources/ChibiSkin');
const mime={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.png':'image/png','.ttf':'font/ttf'};
const server=http.createServer((req,res)=>{try{const file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));if(!file.startsWith(root+path.sep)||!fs.existsSync(file)||fs.statSync(file).isDirectory()){res.writeHead(404);res.end();return;}res.writeHead(200,{'Content-Type':mime[path.extname(file)]||'application/octet-stream'});fs.createReadStream(file).pipe(res);}catch{res.writeHead(400);res.end();}});
function assert(value,message){if(!value)throw new Error(message);}
async function focusIcons(){
  const result={};
  for(const file of ['nav_heroes','item_expbook','nav_gacha','nav_dungeons','nav_quests','nav_shop','nav_map']){
    const {data,info}=await sharp(path.join(assetFolder,file+'.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const {width,height,channels}=info,visited=new Uint8Array(width*height);let biggest=null;
    for(let start=0;start<visited.length;start++){
      if(visited[start]||data[start*channels+3]<64)continue;
      const stack=[start];visited[start]=1;let count=0,x0=width,y0=height,x1=0,y1=0;
      while(stack.length){const p=stack.pop(),x=p%width,y=Math.floor(p/width);count++;x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);for(const [dx,dy] of [[-1,0],[1,0],[0,-1],[0,1]]){const nx=x+dx,ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;const np=ny*width+nx;if(!visited[np]&&data[np*channels+3]>=64){visited[np]=1;stack.push(np);}}}
      if(!biggest||count>biggest.count)biggest={count,x0,y0,x1,y1};
    }
    const scale=38/Math.max(biggest.x1-biggest.x0+1,biggest.y1-biggest.y0+1);
    result[file]={w:width*scale,h:height*scale,x:21-(biggest.x0+biggest.x1)/2*scale,y:21-(biggest.y0+biggest.y1)/2*scale,sourceBounds:[biggest.x0,biggest.y0,biggest.x1,biggest.y1]};
  }
  fs.writeFileSync(path.join(__dirname,'icon-focus.json'),JSON.stringify(result,null,2));
}
async function ready(page,url){await page.goto(url);await page.waitForSelector('.scene');await page.evaluate(async()=>{await document.fonts.ready;await Promise.all(Array.from(document.images).map(i=>i.decode()));});}
async function inspect(page){return page.evaluate(()=>{
  const scene=document.querySelector('.scene'),s=scene.getBoundingClientRect();
  const targets=Array.from(scene.querySelectorAll('button')).filter(e=>!e.disabled&&getComputedStyle(e).visibility!=='hidden'&&e.getBoundingClientRect().width>0).map(e=>{
    const r=e.getBoundingClientRect();let left=r.left,right=r.right,top=r.top,bottom=r.bottom;
    for(let a=e.parentElement;a&&a!==scene;a=a.parentElement){const c=getComputedStyle(a),ar=a.getBoundingClientRect();if(['auto','hidden','scroll'].includes(c.overflowX)){left=Math.max(left,ar.left);right=Math.min(right,ar.right);}if(['auto','hidden','scroll'].includes(c.overflowY)){top=Math.max(top,ar.top);bottom=Math.min(bottom,ar.bottom);}}
    return {name:e.getAttribute('aria-label')||e.textContent.trim(),x:left-s.left,y:top-s.top,w:right-left,h:bottom-top,centered:getComputedStyle(e).textAlign==='center'};
  }).filter(t=>t.w>1&&t.h>1);
  const overlaps=[];for(let i=0;i<targets.length;i++)for(let j=i+1;j<targets.length;j++){const a=targets[i],b=targets[j];if(Math.min(a.x+a.w,b.x+b.w)-Math.max(a.x,b.x)>1&&Math.min(a.y+a.h,b.y+b.h)-Math.max(a.y,b.y)>1)overlaps.push([a.name,b.name]);}
  const dock=scene.querySelector('.bottom-dock'),d=dock?.getBoundingClientRect(),style=dock?getComputedStyle(dock):null;
  return {targets,overlaps,invalid:targets.filter(t=>t.w<47.5||t.h<47.5||t.x<-.5||t.y<-.5||t.x+t.w>s.width+.5||t.y+t.h>s.height+.5),uncentered:targets.filter(t=>!t.centered),dock:d?{bottomGap:s.bottom-d.bottom,leftGap:d.left-s.left,rightGap:s.right-d.right,border:style.borderTopWidth,radius:style.borderTopLeftRadius,dividers:dock.querySelectorAll('.nav-divider').length,entries:dock.querySelectorAll('[data-nav]').length}:null,source3D:scene.querySelector('.stage-photo img')?.getAttribute('src')||null};
});}
async function main(){
  await focusIcons();await new Promise(r=>server.listen(0,'127.0.0.1',r));
  const url=`http://127.0.0.1:${server.address().port}/docs/ui-commercial-review/revision-02/index.html`;
  if(process.argv.includes('--serve')){console.log(url);return;}
  fs.mkdirSync(output,{recursive:true});
  const browser=await chromium.launch({executablePath:process.env.UI_REVIEW_BROWSER||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
  const report={scope:'Static design prototype over existing Unity 3D capture; no live 3D or physical phone acceptance',screenshots:[],checks:[],errors:[]};
  try{
    for(const [name,query,width] of [['home-16x9.png','page=home',800],['home-20x9.png','page=home&ratio=20:9',1000],['home-challenges.png','page=home&detail=challenge',800],['battle-3d-16x9.png','page=battle',800],['battle-3d-20x9.png','page=battle&ratio=20:9',1000],['battle-3d-selected.png','page=battle&detail=card',800]]){
      const page=await browser.newPage({viewport:{width,height:450},deviceScaleFactor:2});page.on('pageerror',e=>report.errors.push(e.message));
      await ready(page,`${url}?export=1&${query}`);const geometry=await inspect(page);
      assert(!geometry.invalid.length,`${name} invalid ${JSON.stringify(geometry.invalid)}`);
      assert(!geometry.overlaps.length,`${name} overlaps ${JSON.stringify(geometry.overlaps)}`);
      assert(!geometry.uncentered.length,`${name} text not centered`);
      if(geometry.dock){assert(geometry.dock.bottomGap===0&&geometry.dock.leftGap===0&&geometry.dock.rightGap===0,'Dock not flush');assert(geometry.dock.border==='0px'&&geometry.dock.radius==='0px'&&geometry.dock.dividers===0,'Dock chrome remains');assert(geometry.dock.entries===6,'Merged navigation not applied');}
      if(query.includes('page=battle'))assert(geometry.source3D,'Missing actual 3D image source');
      await page.locator('.scene').screenshot({path:path.join(output,name),animations:'disabled'});report.screenshots.push({file:name,pixels:[width*2,900],geometry});await page.close();
    }
    const page=await browser.newPage({viewport:{width:1100,height:900},hasTouch:true});await ready(page,url);
    await page.locator('[data-nav="挑戰"]').tap();assert(await page.locator('#challenges').isVisible(),'Challenge menu unavailable');
    for(const label of ['素材副本','世界 Boss']){await page.locator(`[data-demo="${label}"]`).tap();assert(await page.locator('dialog').isVisible(),'Merged destination unavailable');await page.locator('dialog button').tap();}
    await page.locator('[data-nav="養成"]').tap();assert(!await page.locator('#challenges').isVisible(),'Challenge menu did not close');
    await page.locator('[data-page="battle"]').tap();
    await page.locator('[data-card="1"]').tap();assert(await page.locator('.card-detail').isVisible(),'Card detail failed');
    assert(await page.locator('.target-option').count()===3,'Enemy target preview failed');
    await page.locator('[data-target="enemy-1"]').tap();assert(await page.locator('dialog').isVisible(),'3D target preview failed');await page.locator('dialog button').tap();
    await page.locator('[data-squad="0"]').tap();assert(await page.locator('.unit-detail').textContent().then(t=>t.includes('836 / 836')),'Actual capture unit profile mismatch');await page.locator('.unit-detail .detail-close').tap();
    await page.locator('.next').tap();await page.waitForFunction(()=>document.querySelector('.hand-strip').scrollLeft>20);await page.locator('[data-card="6"]').tap();await page.locator('.card-detail .detail-close').tap();
    await page.locator('.battle-menu').tap();assert(await page.locator('.battle-drawer').isVisible(),'Battle menu failed');
    report.checks.push('six-entry navigation, two merged challenge destinations, flush unframed dock, centered button text, aligned original icons, original Unity 3D source, card-to-target, unit detail, last card, battle menu');
    for(const width of [800,1000]){const phone=await browser.newPage({viewport:{width,height:450},deviceScaleFactor:2,hasTouch:true,isMobile:true});await ready(phone,`${url}?export=1&page=battle&ratio=${width===800?'16:9':'20:9'}`);await phone.locator('[data-card="1"]').tap();await phone.locator('[data-target="enemy-1"]').tap();assert(await phone.locator('dialog').isVisible(),'Touch target failed');report.checks.push(`touch emulation ${width}x450: card -> 3D projected enemy target`);await phone.close();}
    assert(!report.errors.length,report.errors.join('; '));fs.writeFileSync(path.join(output,'verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({screenshots:report.screenshots.length,checks:report.checks,errors:report.errors},null,2));await page.close();
  }finally{await browser.close();server.close();}
}
main().catch(e=>{console.error(e);server.close();process.exitCode=1;});
