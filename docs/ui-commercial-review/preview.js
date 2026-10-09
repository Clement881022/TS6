const params = new URLSearchParams(location.search);
const state = { variant: params.get('variant') === 'B' ? 'B' : 'A', ratio: params.get('ratio') === '20:9' ? '20:9' : '16:9', page: params.get('page') === 'battle' ? 'battle' : 'home', selectedCard: null, targeting: false };
const assetRoot = '../../client/Assets/Resources/ChibiSkin/';
const art = (file, cls = '', alt = '') => `<img class="${cls}" src="${assetRoot}${file}.png" alt="${alt}" draggable="false">`;
const navItems = [['武將','nav_heroes'],['養成','item_expbook'],['招募','nav_gacha'],['副本','nav_dungeons'],['世界 Boss','nav_map'],['任務','nav_quests'],['商店','nav_shop']];
const cards = [
  {name:'屏障', face:'face_liubei', cost:1, short:'護盾 111%', text:'為選定友軍提供護盾。', target:'友軍'},
  {name:'基本攻擊', face:'face_r_archer', cost:1, short:'物傷 100%', text:'對選定敵軍造成物理傷害。', target:'敵軍'},
  {name:'基本攻擊', face:'face_liubei', cost:1, short:'法傷 100%', text:'對選定敵軍造成法術傷害。', target:'敵軍'},
  {name:'基本攻擊', face:'face_r_shield', cost:1, short:'物傷 100%', text:'對選定敵軍造成物理傷害。', target:'敵軍'},
  {name:'治療', face:'face_liubei', cost:1, short:'治療 167%', text:'恢復選定友軍的生命值。', target:'友軍'},
  {name:'移動', face:'face_r_sword', cost:0, short:'選擇目的地', text:'選擇我方單位與目的地；正式版依既有移動規則判定。', target:'位置'},
  {name:'移動', face:'face_r_archer', cost:0, short:'選擇目的地', text:'選擇我方單位與目的地；正式版依既有移動規則判定。', target:'位置'}
];
const squad = [{name:'馬商張世平',face:'face_r_villager',hp:'530 / 884',fill:60},{name:'義勇盾兵',face:'face_r_shield',hp:'884 / 884',fill:100},{name:'劉備',face:'face_liubei',hp:'497 / 497',fill:100},{name:'義勇弓兵',face:'face_r_archer',hp:'497 / 497',fill:100}];
const primary = (label = '開始出征', extra = '') => `<button class="primary ${extra}" data-demo="${label}"><span>${label}</span><span class="arrow" aria-hidden="true">›</span></button>`;
const navItem = (item) => `<button class="nav-item" data-nav="${item[0]}" aria-label="${item[0]}"><span class="nav-art">${art(item[1])}</span><span class="nav-label">${item[0]}</span></button>`;
const safe = '<div class="safe-overlay"><span>安全區：左右預留；底部避開系統手勢</span></div>';
function homeScene() {
  return `<div class="scene variant-${state.variant.toLowerCase()} ${state.ratio === '20:9' ? 'wide' : ''}" data-scene="home">
    <div class="backdrop"></div><div class="hud">
      <button class="profile" data-demo="主公帳號" aria-label="主公帳號">${art('face_liubei')}<span class="profile-info"><span class="player-name"><span>主公</span><small>Lv.1</small></span><span class="player-xp">經驗 0/65<span class="xp-track"></span></span></span></button>
      <div class="resources"><div class="resource">${art('item_stamina','', '體力')}<span>62/62</span></div><div class="resource">${art('item_gold','','銅錢')}<span>5,000</span></div><div class="resource">${art('item_yuanbao','','元寶')}<span>2,000</span></div><button class="help" data-demo="主城說明" aria-label="主城說明">?</button></div>
    </div>
    <div class="proposal-watermark">${state.variant} · 主城設計提案</div>
    <section class="campaign" aria-label="主線征戰"><div class="campaign-top"><span>主線征戰</span><span class="chapter">第零章</span></div><h2>涿縣盜匪</h2><p class="next-stage">0-1 · 涿縣村口</p><div class="progress-row"><span>章節進度</span><span>0 / 10</span></div><div class="progress-track"></div>${primary()}<div class="formation-note"><span>排兵布陣</span><span>第 9 關開放</span></div></section>
    <nav class="nav-dock" aria-label="主城功能"><div class="nav-group">${navItems.slice(0,3).map(navItem).join('')}</div><div class="nav-divider"></div><div class="nav-group">${navItems.slice(3,5).map(navItem).join('')}</div><div class="nav-divider"></div><div class="nav-group">${navItems.slice(5).map(navItem).join('')}</div></nav>${safe}</div>`;
}
function boardUnit(file, enemy = false, intent = '') {
  return `<span class="board-unit ${enemy ? 'enemy' : ''}">${art(file)}<span class="unit-health"><i style="width:100%"></i></span>${intent ? `<span class="intent-chip">${intent}</span>` : ''}</span>`;
}
function battleScene() {
  const positions = {2:['face_bandit_archer',true,'攻擊'],4:['face_bandit_archer',true,'攻擊'],9:['face_bandit_shaman',true,'蓄力'],10:['face_r_shield',false],15:['face_r_villager',false],16:['face_liubei',false],18:['face_r_archer',false]};
  const grid = Array.from({length:25},(_,i)=>`<div class="board-cell" data-cell="${i}">${positions[i] ? `<button data-unit="${i}" data-side="${positions[i][1] ? 'enemy' : 'ally'}" aria-label="${positions[i][1] ? '敵軍' : '友軍'}單位">${boardUnit(...positions[i])}</button>` : ''}</div>`).join('');
  return `<div class="scene battle-scene variant-a ${state.ratio === '20:9' ? 'wide' : ''}" data-scene="battle"><div class="backdrop"></div>
    <div class="battle-top"><div class="battle-heading"><strong>0-6 · 護送馬商</strong><span>第 1 回合</span></div><button class="battle-menu" aria-label="戰鬥選單" aria-expanded="false">☰</button></div>
    <div class="proposal-watermark">手機戰鬥 UX 示意 · 棋盤以平面標記代替 3D</div>
    <div class="squad" aria-label="我方隊伍">${squad.map((u,i)=>`<button class="squad-unit" data-squad="${i}" aria-label="查看 ${u.name} 狀態">${art(u.face)}<span class="squad-bar"><i style="width:${u.fill}%"></i></span></button>`).join('')}</div>
    <div class="board-zone" aria-label="棋盤配置示意"><div class="board-grid">${grid}</div></div>
    <div class="battle-instruction"><span>點選手牌查看效果 · 再選目標</span></div>
    <div class="battle-bottom"><div class="hand-area"><button class="hand-arrow previous" aria-label="前面的手牌">‹</button><div class="hand-viewport" aria-label="七張手牌，可左右滑動">${cards.map((c,i)=>`<button class="hand-card" data-card="${i}" aria-label="${c.name}，費用 ${c.cost}，${c.short}"><span class="card-cost">${c.cost}</span>${art(c.face)}<b>${c.name}</b><small>${c.short}</small></button>`).join('')}</div><button class="hand-arrow next" aria-label="後面的手牌">›</button></div><div class="turn-controls"><div class="energy"><span>費用</span><strong>3 / 10</strong></div><button class="primary turn-end">結束回合</button><div class="pile-counts"><span>抽牌 11</span><span>棄牌 0</span></div></div></div>
    <aside class="battle-detail hidden" aria-live="polite"></aside><div class="battle-drawer hidden"><button data-menu-action="重置視角">重置視角</button><button data-menu-action="撤退">撤退</button><button data-menu-action="重來">重來</button><button data-menu-action="戰鬥說明">戰鬥說明</button><p>設計提案：選單沒有接入遊戲。測試用「直接勝利」不列入玩家介面。</p></div>${safe}</div>`;
}
function renderComponents() {
  const token = state.variant === 'A' ? '青玉 #173B38<br>細金 #C6A96B<br>文字 #F6ECD1<br>面板圓角 12 · 按鈕圓角 7' : '青玉 #173C32<br>銅金 #BD955C<br>文字 #F6ECD1<br>面板圓角 22 · 按鈕圓角 24';
  document.querySelector('#components').innerHTML = `<div class="components-scroll"><div class="component-sheet variant-${state.variant.toLowerCase()}"><div class="component-heading"><h2>${state.variant} · ${state.variant === 'A' ? '輕巧青玉金邊' : '圓潤銅金徽章'} / 元件放大</h2><span>設計提案 · 非遊戲截圖</span></div><div class="component-row"><h3>主要操作<br>三種狀態</h3><div class="state-samples"><div class="state-sample"><small>預設</small>${primary()}</div><div class="state-sample"><small>按下</small>${primary('開始出征','pressed-sample')}</div><div class="state-sample"><small>停用</small><button class="primary" disabled>尚未開放</button></div></div></div><div class="component-row"><h3>資訊面板<br>材質與字級</h3><div class="state-samples"><div class="panel-sample"><strong>涿縣盜匪</strong><p>0-1 · 涿縣村口</p><p style="font-size:14px;color:#bdcbbb;margin-top:8px">章節進度 0 / 10</p></div><div class="token-list">${token}<br>標題 25 · 資訊 16 · 輔助 14<br>主要操作高度 48</div></div></div><div class="component-row"><h3>導航圖示<br>原圖沿用，容器新增</h3><div class="icon-samples">${navItems.slice(0,3).map(navItem).join('')}</div></div></div></div>`;
}
function status(text) { document.querySelector('.demo-status').textContent = text; }
function showDialog(title, text) { document.querySelector('#dialog-title').textContent = title; document.querySelector('#dialog-text').textContent = text; document.querySelector('#review-dialog').showModal(); }
function closeDetail() { state.selectedCard = null; state.targeting = false; document.querySelector('.battle-detail')?.classList.add('hidden'); document.querySelector('.hand-area')?.classList.remove('concealed'); document.querySelectorAll('.hand-card,.squad-unit').forEach(e=>e.classList.remove('is-selected')); document.querySelectorAll('.board-cell').forEach(e=>e.classList.remove('target-option')); document.querySelector('.battle-instruction span').textContent = '點選手牌查看效果 · 再選目標'; }
function showCard(index) {
  state.selectedCard = index; state.targeting = true;
  const card = cards[index], detail = document.querySelector('.battle-detail');
  document.querySelectorAll('.hand-card').forEach(e=>e.classList.toggle('is-selected',Number(e.dataset.card) === index));
  document.querySelectorAll('.board-cell').forEach(e=>e.classList.remove('target-option'));
  detail.classList.remove('hidden'); detail.classList.add('card-detail'); document.querySelector('.hand-area').classList.add('concealed');
  detail.innerHTML = `<button class="detail-close" aria-label="取消選牌">×<span>取消</span></button><h2>${card.name} · ${card.short}</h2><p>${card.text}</p><p class="detail-secondary">費用 ${card.cost} · 點選${card.target} · 取消可重新選牌</p>`;
  detail.querySelector('.detail-close').onclick = closeDetail;
  document.querySelector('.battle-instruction span').textContent = `已選 ${card.name} · 點選${card.target}`;
  document.querySelectorAll(`.board-cell:has([data-side="${card.target === '敵軍' ? 'enemy' : 'ally'}"])`).forEach(e=>e.classList.add('target-option'));
  if(card.target === '位置') document.querySelectorAll('.board-cell').forEach(e=>e.classList.add('target-option'));
  status('操作示意：點牌即展開效果並標示目標，再點棋盤；沒有計算傷害或消耗費用。');
}
function fit() {
  const wrap = document.querySelector('#viewport-wrap'), viewport = document.querySelector('#viewport');
  const width = state.ratio === '20:9' ? 1000 : 800;
  if(params.has('export')) { wrap.style.width = `${width}px`; wrap.style.height = '450px'; return; }
  const scale = Math.min(1.4,wrap.clientWidth / width);
  viewport.style.width = `${width}px`; viewport.style.height = '450px'; viewport.style.transform = `scale(${scale})`; wrap.style.height = `${450 * scale}px`;
}
function render() {
  document.querySelector('#viewport').innerHTML = state.page === 'battle' ? battleScene() : homeScene();
  renderComponents();
  document.querySelector('#proposal-title').textContent = state.page === 'battle' ? '手機戰鬥 · 棋盤優先，點選展開' : `${state.variant} · ${state.variant === 'A' ? '輕巧青玉金邊' : '圓潤銅金徽章'}`;
  document.querySelector('#proposal-description').textContent = state.page === 'battle' ? '隊伍縮成頭像血條；手牌固定寬度左右滑動；點牌展開，選目標後收起。' : state.variant === 'A' ? '細邊、低紋理、較輕的面板，讓主城地標與出征成為焦點。' : '圓潤銅金邊框與徽章式圖示，強調 Q 版的親和力與材質厚度。';
  document.querySelector('.variant-controls').style.display = state.page === 'battle' ? 'none' : 'flex';
  document.querySelectorAll('[data-variant]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.variant === state.variant)));
  document.querySelectorAll('[data-ratio]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.ratio === state.ratio)));
  document.querySelectorAll('[data-page]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.page === state.page)));
  document.querySelector('.scene').classList.toggle('show-safe',document.querySelector('#safe-toggle').checked);
  document.querySelectorAll('#viewport [data-demo]').forEach(e=>e.onclick = ()=>showDialog(e.dataset.demo,'這是 UI 設計提案，用來比較版面與操作回饋。此按鈕未連接遊戲功能。'));
  document.querySelectorAll('#viewport [data-nav]').forEach(e=>e.onclick = ()=>{ document.querySelectorAll('#viewport [data-nav]').forEach(x=>x.classList.remove('is-selected')); e.classList.add('is-selected'); status(`已點選「${e.dataset.nav}」入口；這是視覺狀態示意，沒有切換遊戲頁面。`); });
  if(state.page === 'battle') {
    document.querySelectorAll('[data-card]').forEach(e=>e.onclick = ()=>state.selectedCard === Number(e.dataset.card) ? closeDetail() : showCard(Number(e.dataset.card)));
    document.querySelectorAll('[data-squad]').forEach(e=>e.onclick = ()=>{ const unit = squad[Number(e.dataset.squad)], detail = document.querySelector('.battle-detail'); closeDetail(); document.querySelectorAll('.squad-unit').forEach(x=>x.classList.toggle('is-selected',x === e)); detail.classList.remove('hidden','card-detail'); detail.innerHTML = `<button class="detail-close" aria-label="收起單位詳情">×</button><h2>${unit.name}</h2><p>生命 ${unit.hp}</p><p class="detail-secondary">完整狀態改為點按展開，避免常駐占用棋盤空間。</p>`; detail.querySelector('.detail-close').onclick = closeDetail; });
    document.querySelectorAll('[data-cell]').forEach(e=>e.onclick = ()=>{ if(!state.targeting) { if(e.querySelector('[data-unit]')) status('單位點按示意：正式版應在這裡顯示狀態與敵軍行動預告。'); return; } const card = cards[state.selectedCard], side = e.querySelector('[data-side]')?.dataset.side; if(card.target !== '位置' && side !== (card.target === '敵軍' ? 'enemy' : 'ally')) { status(`請點選${card.target}。目標合法性由正式戰鬥規則判斷。`); return; } showDialog('施放確認示意',`「${card.name}」已選擇目標。此原型沒有執行傷害、護盾、治療、移動或費用結算。`); closeDetail(); });
    const hand = document.querySelector('.hand-viewport');
    document.querySelector('.next').onclick = ()=>hand.scrollBy({left:hand.clientWidth*.75,behavior:'smooth'});
    document.querySelector('.previous').onclick = ()=>hand.scrollBy({left:-hand.clientWidth*.75,behavior:'smooth'});
    document.querySelector('.battle-menu').onclick = e=>{ const drawer = document.querySelector('.battle-drawer'); drawer.classList.toggle('hidden'); e.currentTarget.setAttribute('aria-expanded',String(!drawer.classList.contains('hidden'))); };
    document.querySelectorAll('[data-menu-action]').forEach(e=>e.onclick = ()=>showDialog(e.dataset.menuAction,'這是收納次要操作的選單示意，未連接遊戲。撤退與重來在正式版需要二次確認。'));
    document.querySelector('.turn-end').onclick = ()=>showDialog('結束回合','此原型只展示常駐操作位置。正式版仍使用既有回合規則，這裡沒有推進回合。');
    if(params.get('detail') === 'card') showCard(0);
    if(params.get('detail') === 'target') showCard(1);
  }
  fit();
}
document.querySelectorAll('[data-variant]').forEach(e=>e.onclick = ()=>{state.variant = e.dataset.variant; render();});
document.querySelectorAll('[data-ratio]').forEach(e=>e.onclick = ()=>{state.ratio = e.dataset.ratio; render();});
document.querySelectorAll('[data-page]').forEach(e=>e.onclick = ()=>{state.page = e.dataset.page; state.selectedCard = null;state.targeting = false;render();});
document.querySelector('#safe-toggle').onchange = e=>document.querySelector('.scene').classList.toggle('show-safe',e.target.checked);
document.querySelectorAll('.dialog-close,.dialog-done').forEach(e=>e.onclick = ()=>document.querySelector('#review-dialog').close());
if(params.has('export')) document.body.classList.add('export');
if(params.get('export') === 'components') document.body.classList.add('export-components');
render();
window.addEventListener('resize',fit);
document.fonts.ready.then(fit);
