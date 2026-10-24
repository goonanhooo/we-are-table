import {orientationQuaternion,gravityRelativeQuaternion,calibratedLegTilt,calibratedLegYaw} from './orientation.mjs';

const $=selector=>document.querySelector(selector);
const labels=['노란 다리','파란 다리','초록 다리','빨간 다리'];
const legRGB=['242 187 43','66 139 238','60 175 92','233 85 78'];
const actions={
  normal:[],
  giraffe:[{label:'다리 늘이기',symbol:'＋',bit:16},{label:'다리 줄이기',symbol:'−',bit:32}],
  tennis:[{label:'테니스공 접착',symbol:'●',bit:64},{label:'접착 해제',symbol:'○',bit:128}],
  ice:[{label:'얼음',symbol:'❄',bit:16},{label:'풀기',symbol:'↺',bit:32}],
  squid:[{label:'분출',symbol:'↑',bit:16}],
};
const clean=value=>String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'').slice(0,8);
const format=value=>{const code=clean(value);return code.length>4?`${code.slice(0,4)} ${code.slice(4)}`:code;};
let ws,slot=-1,joined=false,unity=false,wanted=false,retry,attempt=0,wake;
let focused=true,gyroActive=false,gyroWanted=false,reference=null,latest=null,lastSensor=0,poseSent=false;
let sensorSource='',sensorError='',sensorRequesting=false;
let motionGravity=null,motionGravityRef=null,motionLastAt=0,motionYaw=0,motionHasGyro=false,sensorRequestId=0;
let tilt={x:0,z:0},yaw=0,lastWrappedYaw=0;
let occupiedSlots=[false,false,false,false];
const pointers=new Map();
let colorsOn=true,currentMode='normal',iceFrozen=true;try{colorsOn=localStorage.getItem('tableColors')!=='off';}catch{}
const message=value=>$('#message').textContent=value;
const transmit=value=>{if(ws?.readyState===WebSocket.OPEN)ws.send(JSON.stringify(value));};
const sendInput=value=>transmit({type:'input',bits:value,mode:currentMode});
const bits=()=>joined&&unity&&focused&&document.visibilityState==='visible'?[...pointers.values()].reduce((a,b)=>a|(gyroActive?b&240:b),0):0;
function colorUI(){
  const plain=currentMode==='normal'||(currentMode==='ice'&&!iceFrozen);
  const model=plain?'start':currentMode;
  const extension=plain?'svg':'png';
  $('#table-image').src=`/assets/table-${model}${colorsOn?'-colors':''}.${extension}`;
  $('#color-toggle').setAttribute('aria-pressed',String(colorsOn));
  $('#color-toggle').style.opacity=colorsOn?'1':'.35';
}
$('#color-toggle').onclick=()=>{colorsOn=!colorsOn;colorUI();try{localStorage.setItem('tableColors',colorsOn?'on':'off');}catch{}};
colorUI();
const sensorFresh=()=>latest&&performance.now()-lastSensor<1000;
const iphone=/iPhone|iPad|iPod/.test(navigator.userAgent||'');
const missingSensorText=()=>iphone?'아이폰 Safari에서 HTTPS 주소를 직접 열고 동작 및 방향 접근을 허용하세요':'센서 신호 없음, 동작/방향 권한을 확인해 주세요';
function gyroUI(){
  const fresh=!!sensorFresh();
  const state=sensorRequesting?'requesting':sensorError?'error':!gyroWanted?'off':!fresh?(gyroActive?'stale':'waiting'):gyroActive?'active':'ready';
  let title={requesting:'자이로 권한 요청 중',error:'자이로 연결 실패',off:'자이로 꺼짐',waiting:'센서 신호 기다리는 중',stale:'센서 신호 끊김',ready:'자이로 켜짐, 수직보정 대기',active:'자이로 켜짐, 다리 조작 중'}[state];
  if(sensorSource==='motion'&&!motionHasGyro&&['ready','active'].includes(state))title=state==='active'?'기울기 센서 켜짐, 다리 조작 중':'기울기 센서 켜짐, 수직보정 대기';
  const detail={requesting:'동작 및 방향 접근을 허용해 주세요',waiting:'폰을 움직여 센서를 연결해 주세요',stale:'센서가 다시 연결되면 이어집니다'}[state]||sensorError;
  $('#gyro-panel').dataset.state=state;
  $('#gyro-title').textContent=title;
  $('#sensor-state').textContent=detail;
  $('#sensor-state').hidden=!detail;
  $('#play').classList.toggle('gyro-active',gyroActive);
  $('#sensor').classList.toggle('active',gyroActive);
  $('#sensor').hidden=sensorRequesting||(gyroWanted&&!sensorError&&state!=='stale');
  $('#sensor').textContent=gyroWanted?'센서 다시 연결':'자이로 켜기';
  $('#recenter').hidden=!gyroWanted;
  $('#touch-switch').hidden=!gyroWanted;
  $('#recenter').disabled=!gyroWanted||!fresh;
  if(gyroActive)showDirection(tilt.x,tilt.z);
}
function showDirection(x,z){$('#tilt-dot').style.transform=`translate(${Math.round(x*35)}cqw,${Math.round(-z*35)}cqw)`;}
function stopPose(){if(poseSent&&joined)transmit({type:'pose',active:false});poseSent=false;}
function release(){pointers.clear();document.querySelectorAll('.pressed').forEach(button=>button.classList.remove('pressed'));showDirection(0,0);if(joined)sendInput(0);stopPose();}
function calibrate(){
  if(!gyroWanted||!sensorFresh()){sensorError='센서 신호가 없습니다';gyroUI();return;}
  if(sensorSource==='motion'){motionGravityRef=motionGravity;latest={w:1,x:0,y:0,z:0};motionYaw=0;motionLastAt=performance.now();}
  reference=latest;gyroActive=true;sensorError='';tilt={x:0,z:0};yaw=0;lastWrappedYaw=0;
  release();gyroUI();
}
function receiveOrientation(event,source){
  if(!Number.isFinite(event.beta)||!Number.isFinite(event.gamma))return;
  if(sensorSource&&sensorSource!==source&&sensorSource!=='motion'&&sensorFresh())return;
  if(sensorSource&&sensorSource!==source){stopPose();gyroActive=false;reference=null;}
  sensorSource=source;
  latest=orientationQuaternion(Number.isFinite(event.alpha)?event.alpha:0,event.beta,event.gamma);
  lastSensor=performance.now();
  sensorError='';
  if(gyroActive&&reference){
    tilt=calibratedLegTilt(reference,latest);
    const wrapped=calibratedLegYaw(reference,latest);
    let delta=wrapped-lastWrappedYaw;
    if(delta>180)delta-=360;if(delta< -180)delta+=360;
    yaw=Math.max(-3600,Math.min(3600,yaw+delta));lastWrappedYaw=wrapped;
  }
  if(gyroWanted)gyroUI();
}
window.addEventListener('deviceorientation',event=>receiveOrientation(event,'relative'));
window.addEventListener('deviceorientationabsolute',event=>receiveOrientation(event,'absolute'));
window.addEventListener('devicemotion',event=>{
  const g=event.accelerationIncludingGravity;
  if(!g||![g.x,g.y,g.z].every(Number.isFinite)||Math.hypot(g.x,g.y,g.z)<1)return;
  if(sensorSource&&sensorSource!=='motion'&&sensorFresh())return;
  const now=performance.now();
  if(sensorSource!=='motion'){
    stopPose();gyroActive=false;reference=null;sensorSource='motion';
    motionGravityRef={x:g.x,y:g.y,z:g.z};motionYaw=0;motionLastAt=now;motionHasGyro=false;
  }
  motionGravity={x:g.x,y:g.y,z:g.z};
  motionHasGyro=motionHasGyro||[event.rotationRate?.alpha,event.rotationRate?.beta,event.rotationRate?.gamma].some(Number.isFinite);
  latest=gravityRelativeQuaternion(motionGravityRef,motionGravity);
  lastSensor=now;sensorError='';
  if(gyroActive&&reference){
    tilt=calibratedLegTilt(reference,latest);
    const dt=Math.max(0,Math.min(.1,(now-motionLastAt)/1000));
    if(Number.isFinite(event.rotationRate?.beta))motionYaw+=event.rotationRate.beta*dt;
    yaw=Math.max(-3600,Math.min(3600,motionYaw));
  }
  motionLastAt=now;
  if(gyroWanted)gyroUI();
});
$('#sensor').onclick=async()=>{
  if(!joined)return;
  if(typeof DeviceOrientationEvent==='undefined'&&typeof DeviceMotionEvent==='undefined'){sensorError='이 브라우저는 동작 센서를 지원하지 않습니다';gyroUI();return;}
  if(window.isSecureContext===false){sensorError='HTTPS 주소에서 다시 열어 주세요';gyroUI();return;}
  sensorRequesting=true;sensorError='';gyroUI();
  try{
    // Start both iOS permission prompts before the first await: the click's
    // transient user activation may be gone by the time a promise settles.
    const permissions=[];
    for(const api of [typeof DeviceOrientationEvent!=='undefined'?DeviceOrientationEvent:null,typeof DeviceMotionEvent!=='undefined'?DeviceMotionEvent:null]){
      if(typeof api?.requestPermission==='function'){
        try{permissions.push(Promise.resolve(api.requestPermission()));}
        catch(error){permissions.push(Promise.reject(error));}
      }
    }
    const results=await Promise.allSettled(permissions);
    if(results.length&&results.every(result=>result.status!=='fulfilled'||result.value!=='granted'))throw Error(iphone?'동작 및 방향 접근을 허용하세요, Safari에서 다시 시도':'브라우저에서 동작/방향 권한을 허용해 주세요');
    gyroWanted=true;gyroActive=false;reference=null;sensorError='';focused=true;
    const request=++sensorRequestId;
    setTimeout(()=>{if(request===sensorRequestId&&gyroWanted&&!sensorFresh()){sensorError=missingSensorText();gyroUI();}},3000);
    try{wake=await navigator.wakeLock?.request('screen');}catch{}
  }catch(error){sensorError=error.message||'센서 권한 오류';}
  finally{sensorRequesting=false;gyroUI();}
};
$('#recenter').onclick=()=>{stopPose();calibrate();};
$('#touch-switch').onclick=()=>{release();gyroActive=false;gyroWanted=false;reference=null;sensorError='';sensorRequestId++;wake?.release();wake=null;gyroUI();};
gyroUI();
function showVisual(visual){
  const nextMode=Object.hasOwn(actions,visual?.mode)?visual.mode:'normal';
  if(nextMode!==currentMode)release();
  currentMode=nextMode;
  iceFrozen=visual?.iceFrozen!==false;
  colorUI();
  actionUI();
}
function actionUI(){
  const choices=actions[currentMode];
  $('#ability-actions').hidden=choices.length===0;
  $('#ability-actions').classList.toggle('single-action',choices.length===1);
  $('#play').classList.toggle('no-actions',choices.length===0);
  for(const [index,id] of ['primary','secondary'].entries()){
    const choice=choices[index],button=$(`#action-${id}`);
    button.hidden=!choice;button.dataset.bit=String(choice?.bit||0);
    button.setAttribute('aria-label',choice?.label||'능력 조작');
    $(`#action-${id}-symbol`).textContent=choice?.symbol||'';
    $(`#action-${id}-label`).textContent=choice?.label||'';
    const on=currentMode==='ice'&&(index===0?iceFrozen:!iceFrozen);
    button.classList.toggle('is-on',on);
    if(currentMode==='ice')button.setAttribute('aria-pressed',String(on));
    else button.removeAttribute('aria-pressed');
  }
}
actionUI();
function showSlot(newSlot){
  slot=newSlot;$('#slot-name').textContent=labels[slot];
  $('#play').style.setProperty('--leg-rgb',legRGB[slot]);
  document.querySelectorAll('[data-leg]').forEach(button=>{
    const mine=Number(button.dataset.leg)===slot;
    button.classList.toggle('mine',mine);button.setAttribute('aria-pressed',String(mine));
  });
  showOccupancy();
  try{localStorage.setItem('preferredLeg',String(slot));}catch{}
}
function showOccupancy(){
  document.querySelectorAll('[data-leg]').forEach(button=>{
    const i=Number(button.dataset.leg),busy=occupiedSlots[i]&&i!==slot;
    button.classList.toggle('busy',busy);button.disabled=busy;
  });
}
for(const button of document.querySelectorAll('[data-leg]'))button.onclick=()=>{
  const target=Number(button.dataset.leg);
  if(!joined||target===slot||occupiedSlots[target])return;
  release();transmit({type:'switch',slot:target});
};
function connect(){
  clearTimeout(retry);if(!wanted)return;
  const code=clean($('#room-code').value);if(code.length!==8){message('방 코드 8자리');wanted=false;return;}
  ws=new WebSocket(`${location.protocol==='https:'?'wss:':'ws:'}//${location.host}/ws?code=${encodeURIComponent(code)}`);
  ws.onopen=()=>{
    let preferredSlot=slot;
    if(preferredSlot<0)try{preferredSlot=Number(localStorage.getItem('preferredLeg')??-1);}catch{}
    transmit({type:'hello',role:'player',code,preferredSlot});
  };
  ws.onmessage=event=>{
    const data=JSON.parse(event.data);
    if(data.type==='joined'){
      joined=true;attempt=0;poseSent=false;$('#entry').hidden=true;$('#play').hidden=false;
      showSlot(data.slot);
      message('');
    }
    if(data.type==='switched'){showSlot(data.slot);message('');}
    if(data.type==='status'){
      if(!data.unity)release();unity=data.unity;
      if(Array.isArray(data.occupiedSlots)&&data.occupiedSlots.length===4){occupiedSlots=data.occupiedSlots;showOccupancy();}
      $('#connection').classList.toggle('online',unity);
      $('#connection').setAttribute('aria-label',unity?'게임 연결됨':'게임 연결 대기');
      $('#game-state').textContent=unity?'게임 연결':'게임 대기';
      showVisual(data.visual);
      if(joined)message('');
    }
    if(data.type==='error')message(data.message);
  };
  ws.onclose=event=>{
    release();joined=false;unity=false;$('#connection').classList.remove('online');
    if(event.code===1008){wanted=false;$('#entry').hidden=false;$('#play').hidden=true;if(!$('#message').textContent)message('방 코드 오류');}
    else if(wanted){retry=setTimeout(connect,Math.min(5000,500*2**attempt++));}
  };
  ws.onerror=()=>{};
}
$('#room-code').addEventListener('input',event=>{event.target.value=format(event.target.value);message('');});
$('#join-form').onsubmit=event=>{event.preventDefault();if(ws&&[WebSocket.OPEN,WebSocket.CONNECTING].includes(ws.readyState))return;wanted=true;connect();};
for(const button of document.querySelectorAll('[data-bit]')){
  button.addEventListener('contextmenu',event=>event.preventDefault());
  button.addEventListener('pointerdown',event=>{
    const bit=Number(button.dataset.bit);if(!joined||!unity||button.hidden||button.disabled||bit===0||(gyroActive&&bit<16))return;
    event.preventDefault();button.setPointerCapture(event.pointerId);pointers.set(event.pointerId,bit);
    button.classList.add('pressed');sendInput(bits());touchDirection();navigator.vibrate?.(8);
  });
  const up=event=>{pointers.delete(event.pointerId);if(![...pointers.values()].includes(Number(button.dataset.bit)))button.classList.remove('pressed');if(joined)sendInput(bits());touchDirection();};
  for(const name of ['pointerup','pointercancel','lostpointercapture'])button.addEventListener(name,up);
}
function touchDirection(){if(gyroActive)return;const b=bits();const x=((b&8)?1:0)-((b&4)?1:0),z=((b&1)?1:0)-((b&2)?1:0),length=Math.max(1,Math.hypot(x,z));showDirection(x/length,z/length);}
window.addEventListener('blur',()=>{focused=false;release();});
window.addEventListener('focus',()=>{focused=true;});
window.addEventListener('pagehide',release);
document.addEventListener('visibilitychange',()=>{if(document.hidden)release();});
setInterval(()=>{if(joined)sendInput(bits());},100);
setInterval(()=>{
  if(joined&&unity&&gyroActive&&document.visibilityState==='visible'&&sensorFresh()){transmit({type:'pose',active:true,x:tilt.x,z:tilt.z,yaw});poseSent=true;}
  else stopPose();
  if(gyroWanted&&!sensorFresh())gyroUI();
},40);
$('#leave').onclick=()=>{wanted=false;clearTimeout(retry);release();ws?.close();wake?.release();wake=null;gyroActive=false;gyroWanted=false;reference=null;sensorError='';sensorRequestId++;gyroUI();$('#play').hidden=true;$('#entry').hidden=false;history.replaceState(null,'',location.pathname+location.search);message('');};
const inviteCode=clean(new URLSearchParams(location.hash.slice(1)).get('code'));
if(inviteCode.length===8){$('#room-code').value=format(inviteCode);wanted=true;connect();}
