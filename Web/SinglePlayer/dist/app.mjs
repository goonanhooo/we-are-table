import * as THREE from './vendor/three.module.js';
import {TablePhysics,LEG_LAYOUT,PARAMETERS,clamp,rotate} from './physics.mjs';
import {MAPS,POWERS,CHECKPOINTS} from './maps.mjs';
import {StoryFlow} from './story.mjs';
import {WorldView} from './world-view.mjs';

const $=id=>document.getElementById(id);
const canvas=$('world'),stage=$('stage');
const heldKeys=new Set(),pointerKeys=new Map();
const aliases={BracketLeft:[3,1],Quote:[3,2],Semicolon:[3,4],Enter:[3,8]};
const keyBits=[1,2,4,8];
const actionKeys=[['KeyE','KeyQ'],['KeyY','KeyR'],['KeyO','KeyU'],['BracketRight','KeyP']];
const actionLabels=[['E','Q'],['Y','R'],['O','U'],[']','P']];
const cards=[],tags=[];
let sim,renderer,scene,camera,tableMesh,legMeshes,cornerDots,jets;
let flow,worldView,walkHeld=false,controlsCollapsed=false,mapWasRunning=true,lastMode='';
let running=true,guideWasRunning=true,colorsOn=true,demo=null,accumulator=0,lastTime=0,fps=60,lastUI=0;
let cameraYaw=2.55,cameraPitch=.49,cameraDistance=4.6;
let toastTimer,drag=null,totalSteps=0,flipDuration=0;
const demoFrames=[{duration:.6,inputs:[0,0,0,0]},{duration:.28,inputs:[2,0,0,0]},{duration:.75,inputs:[4,0,0,0]},{duration:.25,inputs:[0,0,0,0]},{duration:.75,inputs:[8,0,0,0]}];

function toast(text){$('toast').textContent=text;$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').hidden=true,2600);}
function clearInputs(){heldKeys.clear();pointerKeys.clear();walkHeld=false;sim?.inputs.fill(0);for(const key of document.querySelectorAll('.key.held'))key.classList.remove('held');}
function getInputs(){
  const inputs=[0,0,0,0];
  LEG_LAYOUT.forEach((leg,i)=>leg.keys.forEach((key,n)=>{if(heldKeys.has(key))inputs[i]|=keyBits[n];}));
  for(const key of heldKeys)if(aliases[key])inputs[aliases[key][0]]|=aliases[key][1];
  actionKeys.forEach((keys,i)=>{if(heldKeys.has(keys[0]))inputs[i]|=16;if(heldKeys.has(keys[1]))inputs[i]|=32;});
  if(heldKeys.has('Equal')||heldKeys.has('NumpadAdd'))for(let i=0;i<4;i++)inputs[i]|=16;
  if(heldKeys.has('Minus')||heldKeys.has('NumpadSubtract'))for(let i=0;i<4;i++)inputs[i]|=32;
  for(const value of pointerKeys.values())inputs[value.slot]|=value.bit;
  return inputs;
}
function setRunning(value){running=value;accumulator=0;clearInputs();$('paused-overlay').hidden=value;$('pause').ariaLabel=value?'일시정지':'계속하기';$('pause').title=value?'일시정지 · Esc':'계속하기 · Esc';$('pause').innerHTML=value?'<svg viewBox="0 0 24 24"><path d="M9 6v12m6-12v12"/></svg>':'<svg viewBox="0 0 24 24"><path d="m9 5 10 7-10 7z"/></svg>';if(!value)stopDemo(false);}
function resetCamera(){cameraYaw=2.95;cameraPitch=.49;cameraDistance=5.0;}
function stopDemo(showToast=true){if(!demo)return;demo=null;clearInputs();$('demo').innerHTML='<span>▷</span> 움직임 시연 보기';if(showToast)toast('이제 직접 다리를 움직여 보세요.');}
function recover(){if(!sim||flow.locked())return;stopDemo(false);clearInputs();flow.recover();accumulator=0;flipDuration=0;canvas.focus({preventScroll:true});}
function restart(){if(!sim)return;stopDemo(false);clearInputs();flow.restart();accumulator=0;flipDuration=0;setRunning(true);canvas.focus({preventScroll:true});}
function openGuide(){guideWasRunning=running;setRunning(false);$('paused-overlay').hidden=true;$('guide').showModal();}
function closeGuide(){$('guide').close();setRunning(guideWasRunning);canvas.focus({preventScroll:true});}

LEG_LAYOUT.forEach((leg,slot)=>{
  const card=document.createElement('article');card.className='leg-card';card.style.setProperty('--leg-color',leg.color);
  card.innerHTML=`<div class="leg-heading"><span class="dot"></span>${leg.name}<span class="leg-number">0${slot+1}</span></div><div class="key-blocks"><div class="key-block"><span class="key-caption">앞뒤 스윙</span><div class="key-pair"></div></div><div class="key-block"><span class="key-caption">방향 회전</span><div class="key-pair"></div></div></div><div class="leg-footer"><span class="leg-contact">바닥에 닿음</span><span class="leg-angle">0°</span></div>`;
  const pairs=card.querySelectorAll('.key-pair');
  const actionRow=document.createElement('div');actionRow.className='power-key-row';actionRow.innerHTML='<span class="power-key-caption">능력 동작</span><div class="key-pair"></div>';card.insertBefore(actionRow,card.querySelector('.leg-footer'));
  [...leg.labels,...actionLabels[slot]].forEach((label,n)=>{
    const button=document.createElement('button');button.type='button';button.className='key';button.textContent=label;
    const bit=[1,2,4,8,16,32][n];
    button.ariaLabel=`${leg.name} ${['앞으로 스윙','뒤로 스윙','왼쪽 회전','오른쪽 회전','능력 켜기 또는 늘리기','능력 해제 또는 줄이기'][n]} ${label}`;
    button.dataset.bit=bit;button.dataset.slot=slot;
    button.addEventListener('pointerdown',e=>{if(!sim||!running||flow.locked())return;e.preventDefault();stopDemo(false);button.setPointerCapture(e.pointerId);pointerKeys.set(e.pointerId,{slot,bit});});
    const release=e=>pointerKeys.delete(e.pointerId);button.addEventListener('pointerup',release);button.addEventListener('pointercancel',release);button.addEventListener('lostpointercapture',release);
    button.addEventListener('keydown',e=>{if((e.code==='Space'||e.code==='Enter')&&running&&!flow.locked()){e.preventDefault();stopDemo(false);pointerKeys.set(`keyboard-${slot}-${n}`,{slot,bit});}});
    button.addEventListener('keyup',e=>{if(e.code==='Space'||e.code==='Enter')pointerKeys.delete(`keyboard-${slot}-${n}`);});
    button.addEventListener('blur',()=>pointerKeys.delete(`keyboard-${slot}-${n}`));(n<4?pairs[n<2?0:1]:actionRow.querySelector('.key-pair')).append(button);
  });
  $('leg-controls').append(card);cards.push(card);
  const tag=document.createElement('span');tag.className='leg-tag';tag.style.setProperty('--leg-color',leg.color);tag.textContent=slot===3?'↑ ↓ ← →':leg.labels.join(' ');$('labels').append(tag);tags.push(tag);
});

const recognized=new Set([...LEG_LAYOUT.flatMap(l=>l.keys),...Object.keys(aliases),...actionKeys.flat(),'Equal','Minus','NumpadAdd','NumpadSubtract','Space']);
window.addEventListener('keydown',e=>{
  if($('guide').open||$('map-dialog').open||!$('home').hidden)return;
  if(e.code==='KeyC'){e.preventDefault();if(!e.repeat&&running)flow?.interact();return;}
  if(['Digit2','Digit3','Digit4','Digit5'].includes(e.code)){e.preventDefault();if(!e.repeat&&running&&!flow.locked()){clearInputs();flow.selectPower(POWERS[Number(e.code.at(-1))-2].id);}return;}
  if(['Digit1','F1','Escape'].includes(e.code)) {
    e.preventDefault();if(e.repeat)return;
    if(e.code==='Digit1')recover();else if(e.code==='F1')restart();else{setRunning(!running);canvas.focus({preventScroll:true});}return;
  }
  if(!recognized.has(e.code)||e.ctrlKey||e.metaKey||e.altKey)return;
  if(e.code==='Enter'&&e.target.closest('button'))return;
  if(e.code==='Space'&&e.target.closest('button'))return;
  e.preventDefault();if(!running||flow.locked())return;stopDemo(false);heldKeys.add(e.code);
});
window.addEventListener('keyup',e=>heldKeys.delete(e.code));
window.addEventListener('blur',()=>{clearInputs();if(running&&!$('guide').open)setRunning(false);});
document.addEventListener('visibilitychange',()=>{if(document.hidden){clearInputs();if(running&&!$('guide').open)setRunning(false);}});

canvas.addEventListener('pointerdown',e=>{canvas.focus({preventScroll:true});drag={id:e.pointerId,x:e.clientX,y:e.clientY};canvas.setPointerCapture(e.pointerId);});
canvas.addEventListener('pointermove',e=>{if(!drag||drag.id!==e.pointerId)return;cameraYaw-=(e.clientX-drag.x)*.006;cameraPitch=clamp(cameraPitch+(e.clientY-drag.y)*.004,.08,1.4);drag.x=e.clientX;drag.y=e.clientY;});
const endDrag=()=>drag=null;canvas.addEventListener('pointerup',endDrag);canvas.addEventListener('pointercancel',endDrag);
canvas.addEventListener('wheel',e=>{e.preventDefault();cameraDistance=clamp(cameraDistance+e.deltaY*.003,2.3,9);},{passive:false});
canvas.addEventListener('contextmenu',e=>e.preventDefault());
$('pause').addEventListener('click',()=>setRunning(!running));$('resume').addEventListener('click',()=>{setRunning(true);canvas.focus({preventScroll:true});});
$('restart').addEventListener('click',restart);$('recover').addEventListener('click',recover);$('recover-flipped').addEventListener('click',recover);
$('help').addEventListener('click',openGuide);$('close-help').addEventListener('click',closeGuide);$('guide-play').addEventListener('click',closeGuide);
$('guide').addEventListener('cancel',e=>{e.preventDefault();closeGuide();});
$('guide').addEventListener('click',e=>{if(e.target===$('guide')){const r=$('guide').getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)closeGuide();}});
$('camera-home').addEventListener('click',()=>{resetCamera();toast('시점을 처음으로 맞췄어요.');});
$('fullscreen').addEventListener('click',async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen();}catch{toast('이 브라우저에서는 전체 화면을 지원하지 않아요.');}});
$('colors').addEventListener('change',()=>{colorsOn=$('colors').checked;applyColors();});
$('demo').addEventListener('click',()=>{
  if(!sim)return;if(demo){stopDemo();return;}
  restart();setRunning(true);demo={time:0,walk:false};$('demo').innerHTML='<span>□</span> 시연 멈추기';toast('한 다리의 스윙과 회전, 번갈아 걷기를 보여드릴게요.');
});

function changeMap(spec){
  clearInputs();accumulator=0;flipDuration=0;resetCamera();worldView?.build(spec);
  $('map-name').textContent=spec.name;document.body.dataset.map=spec.id;
  $('demo').hidden=spec.id!=='FlatGround';$('chapter').textContent=`${spec.chapter} · ${spec.name}`;
  $('checkpoint-note').textContent=flow?.practice?'자유 연습':'진행 자동 저장';
}
function enterMap(id){stopDemo(false);$('home').hidden=true;if($('map-dialog').open)$('map-dialog').close();flow.enter(id,{practice:['FlatGround','Soccer','Race'].includes(id)});setRunning(true);canvas.focus({preventScroll:true});}
function openMaps(){mapWasRunning=running;setRunning(false);$('paused-overlay').hidden=true;$('map-dialog').showModal();$('checkpoint-list').textContent=`마지막 저장 · ${CHECKPOINTS[flow.save.checkpoint]?.[1]||'복도'}${flow.save.bestRace?' · 최고 기록 '+flow.save.bestRace.toFixed(2)+'초':''}`;}
function closeMaps(){$('map-dialog').close();setRunning(mapWasRunning&&$('home').hidden);if(!$('home').hidden)$('paused-overlay').hidden=true;}
$('map-select').addEventListener('click',openMaps);$('home-maps').addEventListener('click',openMaps);$('close-maps').addEventListener('click',closeMaps);
$('map-dialog').addEventListener('cancel',e=>{e.preventDefault();closeMaps();});
$('new-journey').addEventListener('click',()=>{$('home').hidden=true;flow.start();setRunning(true);canvas.focus({preventScroll:true});});
$('continue-journey').addEventListener('click',()=>{$('home').hidden=true;flow.resume();setRunning(true);canvas.focus({preventScroll:true});});
document.querySelector('.brand').addEventListener('click',e=>{e.preventDefault();setRunning(false);$('paused-overlay').hidden=true;$('home').hidden=false;});
$('interact').addEventListener('click',()=>{if(running)flow.interact();canvas.focus({preventScroll:true});});
$('walk').addEventListener('pointerdown',e=>{if(!running||flow.locked())return;e.preventDefault();$('walk').setPointerCapture(e.pointerId);walkHeld=true;});
for(const event of ['pointerup','pointercancel','lostpointercapture'])$('walk').addEventListener(event,()=>walkHeld=false);
$('walk').addEventListener('keydown',e=>{if((e.code==='Space'||e.code==='Enter')&&running&&!flow.locked()){e.preventDefault();walkHeld=true;}});
$('walk').addEventListener('keyup',()=>walkHeld=false);$('walk').addEventListener('blur',()=>walkHeld=false);
$('collapse-controls').addEventListener('click',()=>{controlsCollapsed=!controlsCollapsed;document.querySelector('.controls-wrap').classList.toggle('collapsed',controlsCollapsed);$('collapse-controls').textContent=controlsCollapsed?'펴기':'접기';});
MAPS.forEach(map=>{const button=document.createElement('button');button.className='map-card';button.style.setProperty('--map-color',map.sky);button.innerHTML=`<span class="map-thumb"><span>${map.chapter}</span></span><strong>${map.name}</strong><small>${map.description}</small>`;button.addEventListener('click',()=>enterMap(map.id));$('map-list').append(button);});
POWERS.forEach(power=>{const button=document.createElement('button');button.dataset.power=power.id;button.className='power-button';button.style.setProperty('--power-color',power.color);button.innerHTML=`<kbd>${power.key}</kbd> ${power.name}`;button.addEventListener('click',()=>{if(running&&!flow.locked()){clearInputs();flow.selectPower(power.id);canvas.focus({preventScroll:true});}});$('power-dock').append(button);});
const allStretch=document.createElement('span');allStretch.className='all-stretch';allStretch.innerHTML='<button id="all-longer" aria-label="모든 다리 늘리기">= 늘리기</button><button id="all-shorter" aria-label="모든 다리 줄이기">− 줄이기</button>';$('power-dock').append(allStretch);
for(const [id,code] of [['all-longer','Equal'],['all-shorter','Minus']]){
  const b=$(id);b.addEventListener('pointerdown',e=>{if(!running||flow.locked())return;e.preventDefault();b.setPointerCapture(e.pointerId);heldKeys.add(code);});for(const event of ['pointerup','pointercancel','lostpointercapture'])b.addEventListener(event,()=>heldKeys.delete(code));
}

function woodTexture(){
  const c=document.createElement('canvas');c.width=c.height=512;const ctx=c.getContext('2d');ctx.fillStyle='#966e48';ctx.fillRect(0,0,512,512);
  let seed=141;function random(){seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;}
  for(let i=0;i<1600;i++){
    const y=random()*512;ctx.beginPath();ctx.strokeStyle=`rgba(${random()>.5?'53,32,15':'229,185,118'},${random()*.08})`;ctx.lineWidth=random()*1.8;
    for(let x=0;x<=512;x+=16){const offset=Math.sin(x*.018+y*.044)*1.8+Math.sin(x*.06+y)*.4;if(x===0)ctx.moveTo(x,y+offset);else ctx.lineTo(x,y+offset);}ctx.stroke();
  }
  const tex=new THREE.CanvasTexture(c);tex.colorSpace=THREE.SRGBColorSpace;tex.anisotropy=8;return tex;
}
function makeScene(){
  renderer=new THREE.WebGLRenderer({canvas,antialias:true,alpha:false,powerPreference:'high-performance'});
  renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
  renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.18;
  scene=new THREE.Scene();scene.background=new THREE.Color('#d6dfcc');scene.fog=new THREE.Fog('#d6dfcc',15,58);
  camera=new THREE.PerspectiveCamera(38,1,.05,250);
  const sky=new THREE.HemisphereLight('#fffdf0','#929e7c',2.2);scene.add(sky);
  const sun=new THREE.DirectionalLight('#ffebd1',3.2);sun.position.set(-5,10,-4);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);
  sun.shadow.camera.left=-10;sun.shadow.camera.right=10;sun.shadow.camera.top=10;sun.shadow.camera.bottom=-10;sun.shadow.camera.near=.1;sun.shadow.camera.far=35;
  sun.shadow.normalBias=.015;sun.shadow.bias=-.00005;scene.add(sun,sun.target);scene.userData.sun=sun;
  const wood=new THREE.MeshStandardMaterial({map:woodTexture(),color:'#e3c69e',roughness:.83});
  tableMesh=new THREE.Group();const top=new THREE.Mesh(new THREE.BoxGeometry(1.4,.1,1.4),wood);top.castShadow=true;top.receiveShadow=true;tableMesh.add(top);
  const edge=new THREE.LineSegments(new THREE.EdgesGeometry(top.geometry),new THREE.LineBasicMaterial({color:'#8b6848',transparent:true,opacity:.3}));tableMesh.add(edge);scene.add(tableMesh);
  cornerDots=LEG_LAYOUT.map(leg=>{const dot=new THREE.Mesh(new THREE.CircleGeometry(.023,24),new THREE.MeshBasicMaterial({color:leg.color}));dot.rotation.x=-Math.PI/2;dot.position.set(leg.x,.051,leg.z);tableMesh.add(dot);return dot;});
  legMeshes=LEG_LAYOUT.map(leg=>{
    const group=new THREE.Group();
    const material=new THREE.MeshStandardMaterial({map:wood.map,color:leg.color,roughness:.9});
    const mesh=new THREE.Mesh(new THREE.BoxGeometry(.12,.7,.12),material);mesh.castShadow=true;mesh.receiveShadow=true;group.add(mesh);
    const outline=new THREE.LineSegments(new THREE.EdgesGeometry(mesh.geometry),new THREE.LineBasicMaterial({color:'#716347',transparent:true,opacity:.12}));group.add(outline);
    const ball=new THREE.Mesh(new THREE.SphereGeometry(.17,16,12),new THREE.MeshStandardMaterial({color:'#d2e294',roughness:.8}));ball.position.y=-.35;ball.visible=false;group.add(ball);
    scene.add(group);return {group,material,ball};
  });
  jets=LEG_LAYOUT.map(()=>{const jet=new THREE.Mesh(new THREE.ConeGeometry(.13,.8,10),new THREE.MeshBasicMaterial({color:'#b4e9ef',transparent:true,opacity:.55,depthWrite:false}));jet.visible=false;scene.add(jet);return jet;});
  worldView=new WorldView(scene,wood.map);
  applyColors();new ResizeObserver(resize).observe(stage);resize();
}
function applyColors(){
  if(!legMeshes)return;
  const tint=sim?.mode==='ice'?'#b1e0ec':sim?.mode==='squid'?'#bea7d9':sim?.mode==='giraffe'?'#e6c88d':null;
  legMeshes.forEach((mesh,i)=>mesh.material.color.set(tint||(colorsOn?LEG_LAYOUT[i].color:'#e3c69e')));
  if(tableMesh)tableMesh.children[0].material.color.set(tint||'#e3c69e');
  cornerDots.forEach(dot=>dot.visible=colorsOn);
}
function resize(){
  if(!renderer)return;const w=stage.clientWidth,h=stage.clientHeight;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();
}
function sampleDemo(){
  let t=demo.time;const previewLength=demoFrames.reduce((sum,frame)=>sum+frame.duration,0);
  if(t<previewLength){for(const frame of demoFrames){if(t<frame.duration)return frame.inputs;t-=frame.duration;}}
  if(!demo.walk){sim.restart();demo.walk=true;demo.time=previewLength;t=0;}
  const walkTime=demo.time-previewLength;
  if(walkTime>5.16){stopDemo();return [0,0,0,0];}
  const cycle=walkTime%.43;
  return cycle<.13?[2,0,0,2]:cycle<.26?[1,2,2,1]:cycle<.39?[0,1,1,0]:[0,0,0,0];
}
const projection=new THREE.Vector3();
function renderScene(time){
  const p=sim.board.translation();tableMesh.position.copy(p);tableMesh.quaternion.copy(sim.board.rotation());
  sim.legs.forEach((leg,i)=>{
    legMeshes[i].group.position.copy(leg.body.translation());legMeshes[i].group.quaternion.copy(leg.body.rotation());legMeshes[i].group.scale.y=leg.length/.7;
    const ball=legMeshes[i].ball;ball.visible=sim.mode==='tennis';ball.scale.y=.7/leg.length;ball.material.color.set(leg.attachment?'#83bc74':'#d2e294');
    const tip=sim.foot(leg),up=rotate(leg.body.rotation(),{x:0,y:1,z:0}),jet=jets[i];jet.position.set(tip.x-up.x*.4,tip.y-up.y*.4,tip.z-up.z*.4);jet.quaternion.copy(leg.body.rotation());jet.scale.setScalar(.9+Math.sin(time*.04+i)*.12);
    jet.visible=sim.mode==='squid'&&running&&!flow.locked()&&!!((sim.inputs[i]&16)||walkHeld||heldKeys.has('Space'));
  });
  worldView.update(sim,flow,time);
  const mobile=stage.clientWidth<701;
  const longest=Math.max(...sim.legs.map(l=>l.length));
  const focus=new THREE.Vector3(p.x,p.y-Math.max(.25,longest*.43),p.z);
  const compact=!mobile && stage.clientHeight<600;
  const r=(cameraDistance+Math.max(0,longest-.7)*.9)*(mobile?1.15:compact?1.12:1);
  camera.position.set(focus.x+Math.sin(cameraYaw)*Math.cos(cameraPitch)*r,focus.y+Math.sin(cameraPitch)*r,focus.z+Math.cos(cameraYaw)*Math.cos(cameraPitch)*r);
  if(flow.map!=='Space'&&flow.map!=='Hallway')camera.position.y=Math.max(.15,camera.position.y);camera.lookAt(focus);
  // Shift the framing upward so the table remains visible above the control pads.
  const fraction=controlsCollapsed?.08:mobile?.28:compact?.23:.18;
  camera.setViewOffset(stage.clientWidth,stage.clientHeight,0,stage.clientHeight*fraction,stage.clientWidth,stage.clientHeight);
  if(flow.cutscene?.type==='ending'){
    const lengths=[10,13,12,14,12,21],positions=[[-3,2.2,0],[-2,1.2,3],[2,1.4,3],[3,2.7,-2],[-3,1.4,6],[0,2.8,-1]];let t=flow.cutscene.time,index=0;
    while(index<5&&t>lengths[index]){t-=lengths[index];index++;}
    camera.clearViewOffset();camera.position.set(...positions[index]);camera.lookAt(0,.85,4.6);
  }
  const sun=scene.userData.sun;sun.position.set(p.x-5,p.y+10,p.z-4);sun.target.position.set(p.x,p.y,p.z);
  renderer.render(scene,camera);
  sim.legs.forEach((leg,i)=>{
    const pos=sim.foot(leg);projection.set(pos.x,pos.y+.05,pos.z);
    const dir=rotate(sim.board.rotation(),{x:LEG_LAYOUT[i].x*.4,y:0,z:LEG_LAYOUT[i].z*.4});projection.x+=dir.x;projection.z+=dir.z;
    projection.project(camera);const x=(projection.x*.5+.5)*stage.clientWidth,y=(-projection.y*.5+.5)*stage.clientHeight;
    tags[i].style.left=`${x}px`;tags[i].style.top=`${y+14}px`;tags[i].hidden=!!flow.cutscene||projection.z>1||x<0||x>stage.clientWidth||y>stage.clientHeight-(controlsCollapsed?65:mobile?375:275)||y<0;
  });
}
function updateUI(){
  const state=sim.state();$('distance').innerHTML=`${state.distance.toFixed(1)}<small>m</small>`;$('fps').textContent=Math.round(fps);
  const objective=flow.objective();$('objective-title').textContent=objective.title;$('objective-hint').textContent=objective.hint;
  const target=flow.target();$('target-hint').textContent=target&&!flow.cutscene&&!flow.ending?`목표까지 ${Math.hypot(target[0]-state.x,target[2]-state.z).toFixed(1)}m`:'';
  $('power-dock').style.bottom=controlsCollapsed?'70px':'';
  $('interact').textContent=flow.cutscene?'C · 연출 건너뛰기':'C · 상호작용';
  for(const button of $('power-dock').querySelectorAll('[data-power]')){const power=POWERS.find(p=>p.id===button.dataset.power);button.disabled=!(flow.powers&power.bit)||flow.locked();button.classList.toggle('selected',sim.mode===power.id);}
  allStretch.hidden=sim.mode!=='giraffe';
  if(lastMode!==sim.mode){lastMode=sim.mode;applyColors();}
  $('status').classList.toggle('flipped',state.up<.2);
  $('status').lastElementChild.textContent=demo?'시연 중':!running?'일시정지':state.up<.2?'테이블이 뒤집혔어요':state.speed>.12?'움직이는 중':'자유롭게 움직여 보세요';
  $('recover-prompt').hidden=flipDuration<1.6||!running||!!demo||flow.locked()||flow.map==='Space';
  state.legs.forEach((leg,i)=>{
    cards[i].classList.toggle('engaged',!!leg.keys);cards[i].querySelector('.leg-angle').textContent=`${Math.round(leg.swing)}°`;
    const contact=cards[i].querySelector('.leg-contact');contact.textContent=leg.grounded?'바닥에 닿음':'다리 들림';contact.classList.toggle('air',!leg.grounded);
    cards[i].querySelector('.power-key-caption').textContent=sim.mode==='giraffe'?`신축 · ${leg.length.toFixed(1)}m`:sim.mode==='tennis'?(leg.attached?'붙음 · 해제 가능':'붙이기 / 떼기'):sim.mode==='squid'?'분사 / 멈춤':'능력 동작';
    for(const button of cards[i].querySelectorAll('.key')){button.classList.toggle('held',!!(leg.keys&Number(button.dataset.bit)));if(Number(button.dataset.bit)>=16)button.disabled=!['giraffe','tennis','squid'].includes(sim.mode);}
  });
  // A readable DOM snapshot also supports inspection without reaching into the simulation.
  canvas.dataset.position=`${state.x.toFixed(3)},${state.y.toFixed(3)},${state.z.toFixed(3)}`;
  canvas.dataset.swings=state.legs.map(l=>l.swing.toFixed(2)).join(',');canvas.dataset.spins=state.legs.map(l=>l.spin.toFixed(2)).join(',');canvas.dataset.steps=totalSteps;
  canvas.dataset.map=flow.map;canvas.dataset.quest=flow.quest;canvas.dataset.checkpoint=flow.save.checkpoint;canvas.dataset.mode=sim.mode;
}
function frame(time){
  const dt=lastTime?Math.min((time-lastTime)/1000,.05):0;lastTime=time;
  if(dt>0)fps+=(1/dt-fps)*.04;
  if(running){
    accumulator+=dt;
    while(accumulator>=PARAMETERS.step){
      const inputs=flow.locked()?[0,0,0,0]:demo?sampleDemo():getInputs();
      const assist=!flow.locked()&&(heldKeys.has('Space')||walkHeld)?flow.assistDirection():null;
      sim.step(inputs,assist);flow.update(PARAMETERS.step);totalSteps++;if(demo)demo.time+=PARAMETERS.step;
      flipDuration=sim.state().up<.2?flipDuration+PARAMETERS.step:0;accumulator-=PARAMETERS.step;
    }
  }
  renderScene(time);if(time-lastUI>80){updateUI();lastUI=time;}
  requestAnimationFrame(frame);
}
async function boot(){
  try {
    sim=await TablePhysics.create();makeScene();
    let storage=null;try{storage=localStorage;}catch{}
    flow=new StoryFlow(sim,{storage,onMap:changeMap,onMessage:toast});flow.enter('Hallway',{practice:true});
    for(let i=0;i<60;i++)sim.step([0,0,0,0]);
    $('loading').hidden=true;setRunning(false);$('paused-overlay').hidden=true;requestAnimationFrame(frame);
  }catch(error){
    console.error(error);$('loading').innerHTML='<strong>3D 화면을 시작하지 못했어요.</strong><p>WebGL을 지원하는 최신 Chrome 또는 Edge에서 다시 열어 주세요.</p><button class="primary-button" onclick="location.reload()">다시 시도</button>';
    $('loading').dataset.error=String(error);
  }
}
boot();
