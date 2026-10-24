// 손그림 끓는 선 — 지우개 대소동(eraser) public/hand-borders.js를 이 컨트롤러에 맞게 줄인 것.
// Every box in BOXES (buttons, inputs, panels) gets its border redrawn as a slightly wobbly
// hand-inked line, and its colour is painted with the very same wobbly outline, so the fill never
// shows outside the line. Three drawings, each a little different, are stacked in one SVG sprite;
// the shared step animation in style.css (@property --hby + @keyframes hand-boil, about 4 fps)
// shows one third at a time, so the line "boils". Kept from the original for speed: no SVG
// filters, only boxes on screen animate, nothing moves with prefers-reduced-motion, and a box that
// has not changed reuses its decoded drawing. The drawing is only the box's own background: no
// layers are added over anything, so touch and pointer handling stay exactly as they were.
const FRAMES=3,BOIL='data-hand-boil';
const BOXES='button,input,.phone-invite,.pc-invite,.watch-invite,.host-slot,#message';
// A chosen box (my leg, the current ice state) gets a thicker line instead of an inner outline.
const CHOSEN='.mine,.is-on';
const PROPS=['background-image','background-size','background-position','background-repeat','background-origin','background-color','border-color','box-shadow'];
const px=v=>parseFloat(v)||0,f=v=>v.toFixed(1);
const visible=c=>!!c&&c!=='transparent'&&!/rgba\([^)]*,\s*0\)$/.test(c);
function hash(s){let h=2166136261;for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619);}return h>>>0;}
function rng(seed){let a=seed>>>0;return()=>{a=a+0x6D2B79F5>>>0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
// 삐뚤빼뚤: each side wavers about a pixel each way (a little more on long sides), easing to
// nothing at the ends so the rounded corners still meet it.
const wobbleOf=len=>Math.min(1.5,.6+len/380);
function wobbly(x0,y0,x1,y1,rand){
 const len=Math.hypot(x1-x0,y1-y0),amp=wobbleOf(len),nx=-(y1-y0)/(len||1),ny=(x1-x0)/(len||1);
 const k1=1+rand()*1.5,k2=2.5+rand()*2,p1=rand()*6.28,p2=rand()*6.28,steps=Math.max(2,Math.round(len/6));let d='';
 for(let i=1;i<=steps;i++){const t=i/steps,o=amp*Math.sin(Math.PI*t)*(.7*Math.sin(k1*Math.PI*t+p1)+.3*Math.sin(k2*Math.PI*t+p2));d+=`L${f(x0+(x1-x0)*t+nx*o)} ${f(y0+(y1-y0)*t+ny*o)}`;}
 return d;
}
function wobblyRect(x0,y0,x1,y1,r,seed){
 r=Math.max(0,Math.min(r,(x1-x0)/2,(y1-y0)/2));const rand=rng(seed),arc=(x,y)=>`A${f(r)} ${f(r)} 0 0 1 ${f(x)} ${f(y)}`;
 return `M${f(x0+r)} ${f(y0)}${wobbly(x0+r,y0,x1-r,y0,rand)}${arc(x1,y0+r)}${wobbly(x1,y0+r,x1,y1-r,rand)}${arc(x1-r,y1)}${wobbly(x1-r,y1,x0+r,y1,rand)}${arc(x0,y1-r)}${wobbly(x0,y1-r,x0,y0+r,rand)}${arc(x0+r,y0)}Z`;
}
// Pencil grain without a filter: the stroke breaks for a hair now and then, and a fainter,
// thinner second stroke rides just beside it so the width seems to change along the line.
function grain(d,color,width,rand){
 const dash=Array.from({length:6},(_,i)=>f(i%2?(.35+rand()*.7)*width:6+rand()*14)).join(' ');
 return `<path d="${d}" fill="none" stroke="${color}" stroke-width="${width.toFixed(2)}" stroke-linejoin="round" stroke-linecap="round" stroke-dasharray="${dash}"/>`+
  `<path d="${d}" fill="none" stroke="${color}" stroke-width="${(width*.5).toFixed(2)}" stroke-linejoin="round" stroke-opacity=".4" transform="translate(${f(rand()*.8-.4)} ${f(rand()*.8-.4)})"/>`;
}
// Only boxes on screen boil, each started on one shared 750 ms beat so they all step together.
const onScreen=new IntersectionObserver(entries=>{for(const e of entries){
 if(e.isIntersecting)e.target.style.setProperty('animation-delay',`${-(performance.now()%750).toFixed(0)}ms`,'important');
 e.target.toggleAttribute(BOIL,e.isIntersecting);
}});
const state=new WeakMap(),decoded=new Set(),pending=new Set();
const plain=el=>{for(const p of PROPS)el.style.removeProperty(p);};
function drop(el){plain(el);el.removeAttribute(BOIL);el.style.removeProperty('animation-delay');onScreen.unobserve(el);const s=state.get(el);if(s)s.shown=null;}
function show(el,s,url){
 const set=(p,v)=>el.style.setProperty(p,v,'important');
 set('background-image',url);set('background-size',`100% ${FRAMES*100}%`);set('background-position','0 var(--hby,0%)');
 set('background-repeat','no-repeat');set('background-origin','border-box');set('background-color','transparent');set('border-color','transparent');
 if(s.chosen)set('box-shadow','none');
 if(!s.shown)onScreen.observe(el);s.shown=url;
}
function draw(el){
 if(!el.isConnected)return;
 plain(el); // read the box's own colours, then draw over them in the same task (no flicker)
 const cs=getComputedStyle(el),w=el.offsetWidth,h=el.offsetHeight,bw=px(cs.borderTopWidth);
 if(w<16||h<16||cs.borderTopStyle==='none'||!bw||!visible(cs.borderTopColor)){drop(el);return;}
 let s=state.get(el);
 if(!s){const at=el.parentElement?[...el.parentElement.children].indexOf(el):0;s={seed:hash(el.id+'|'+el.tagName+'|'+at+'|'+(el.textContent||'').slice(0,40)),shown:null};state.set(el,s);}
 const chosen=el.matches(CHOSEN),inset=/inset/.test(cs.boxShadow)?cs.boxShadow.match(/rgba?\([^)]*\)|#[0-9a-f]{3,8}/i)?.[0]:null;
 const color=chosen&&inset||cs.borderTopColor,fill=visible(cs.backgroundColor)?cs.backgroundColor:'';
 const key=[w,h,bw,color,fill,cs.borderTopLeftRadius,chosen].join('|');
 if(s.key!==key){
  // The line sits just inside the box; the colour is painted with the same path, so it fills
  // right up to the line as it moves and never past it.
  const width=chosen?2.6:Math.max(1.05,bw*.9),m=Math.max(width/2+.2,bw-width/2)+wobbleOf(Math.max(w,h))*.6,r=Math.min(px(cs.borderTopLeftRadius),Math.min(w,h)/2);
  const body=Array.from({length:FRAMES},(_,i)=>{const seed=s.seed+i*7919,d=wobblyRect(m,m,w-m,h-m,r-m,seed);return `<g transform="translate(0 ${h*i})">${fill?`<path d="${d}" fill="${fill}"/>`:''}${grain(d,color,width,rng(seed^0x9e37))}</g>`;}).join('');
  s.url=`url("data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h*FRAMES}" viewBox="0 0 ${w} ${h*FRAMES}">${body}</svg>`)}")`;
  s.key=key;s.chosen=chosen;
 }
 const url=s.url;
 if(decoded.has(url)){show(el,s,url);return;}
 // A new drawing waits until it is decoded; meanwhile the last one (or the plain border) stays.
 if(s.shown)show(el,s,s.shown);
 const img=new Image();img.src=url.slice(5,-2);
 img.decode().catch(()=>{}).then(()=>{decoded.add(url);if(s.url===url&&el.isConnected&&el.offsetWidth)show(el,s,url);});
}
let queued=false;
const later=el=>{pending.add(el);if(!queued){queued=true;queueMicrotask(()=>{queued=false;const list=[...pending];pending.clear();for(const item of list)draw(item);});}};
// Size changes (and a hidden box being shown) redraw; so do class / hidden / disabled / pressed
// changes on the box itself. Same-value writes and the d-pad's .pressed toggle are ignored
// (the pressed look is plain CSS, so a held button never waits for a drawing).
const sizes=new ResizeObserver(entries=>{for(const e of entries)later(e.target);});
const watched=new WeakSet();
const boxesIn=n=>n.nodeType!==1?[]:[...(n.matches(BOXES)?[n]:[]),...n.querySelectorAll(BOXES)];
const watch=n=>{for(const el of boxesIn(n))if(!watched.has(el)){watched.add(el);sizes.observe(el);}};
const unwatch=n=>{for(const el of boxesIn(n)){watched.delete(el);sizes.unobserve(el);onScreen.unobserve(el);}};
const classes=v=>String(v??'').split(/\s+/).filter(c=>c&&c!=='pressed').join(' ');
new MutationObserver(records=>{for(const m of records){
 if(m.type==='childList'){m.removedNodes.forEach(unwatch);m.addedNodes.forEach(watch);continue;}
 if(!watched.has(m.target))continue;
 const now=m.target.getAttribute(m.attributeName);
 if(m.attributeName==='class'?classes(now)===classes(m.oldValue):now===m.oldValue)continue;
 later(m.target);
}}).observe(document.body,{subtree:true,childList:true,attributes:true,attributeOldValue:true,attributeFilter:['class','hidden','disabled','aria-pressed']});
watch(document.body);
