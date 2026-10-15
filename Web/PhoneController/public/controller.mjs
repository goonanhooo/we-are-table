import {orientationQuaternion,gravityRelativeQuaternion,calibratedLegTilt,calibratedLegYaw} from './orientation.mjs';

const $=selector=>document.querySelector(selector);
const labels=['01 · 노랑','02 · 파랑','03 · 초록','04 · 빨강'];
const clean=value=>String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'').slice(0,8);
const format=value=>{const code=clean(value);return code.length>4?`${code.slice(0,4)} ${code.slice(4)}`:code;};
let ws,slot=-1,joined=false,unity=false,wanted=false,retry,attempt=0,wake;
let focused=true,gyroActive=false,gyroWanted=false,reference=null,latest=null,lastSensor=0,poseSent=false;
let sensorSource='',sensorError='',sensorRequesting=false,sensorSamples=0;
let motionGravity=null,motionGravityRef=null,motionLastAt=0,motionYaw=0,motionHasGyro=false,sensorRequestId=0;
let tilt={x:0,z:0},yaw=0,lastWrappedYaw=0;
let occupiedSlots=[false,false,false,false];
const pointers=new Map();
let colorsOn=true,currentMode='normal';try{colorsOn=localStorage.getItem('tableColors')!=='off';}catch{}
const message=value=>$('#message').textContent=value;
const transmit=value=>{if(ws?.readyState===WebSocket.OPEN)ws.send(JSON.stringify(value));};
const bits=()=>joined&&unity&&focused&&document.visibilityState==='visible'?[...pointers.values()].reduce((a,b)=>a|(gyroActive?b&240:b),0):0;
function colorUI(){
  $('#table-image').src=`/assets/table-${currentMode}${colorsOn?'-colors':''}.png`;
  $('#leg-key').hidden=!colorsOn;
  $('#color-toggle').setAttribute('aria-pressed',String(colorsOn));
  $('#color-toggle').style.opacity=colorsOn?'1':'.35';
}
$('#color-toggle').onclick=()=>{colorsOn=!colorsOn;colorUI();try{localStorage.setItem('tableColors',colorsOn?'on':'off');}catch{}};
colorUI();
const sensorFresh=()=>latest&&performance.now()-lastSensor<1000;
const iphone=/iPhone|iPad|iPod/.test(navigator.userAgent||'');
const missingSensorText=()=>iphone?'아이폰 Safari에서 HTTPS 주소를 직접 열고 동작 및 방향 접근을 허용하세요':'센서 신호 없음 · 동작/방향 권한을 확인해 주세요';
function gyroUI(){
  const fresh=!!sensorFresh();
  const state=sensorRequesting?'requesting':sensorError?'error':!gyroWanted?'off':!fresh?(gyroActive?'stale':'waiting'):gyroActive?'active':'ready';
  let title={requesting:'자이로 권한 요청 중',error:'자이로 연결 실패',off:'자이로 꺼짐',waiting:'센서 신호 기다리는 중',stale:'센서 신호 끊김',ready:'자이로 켜짐 · 수직보정 대기',active:'자이로 켜짐 · 다리 조작 중'}[state];
  if(sensorSource==='motion'&&!motionHasGyro&&['ready','active'].includes(state))title=state==='active'?'기울기 센서 켜짐 · 다리 조작 중':'기울기 센서 켜짐 · 수직보정 대기';
  const detail={requesting:'휴대폰의 동작/방향 권한을 허용하세요',off:'폰을 움직여 다리를 조작하려면 켜세요',waiting:'휴대폰을 움직여 센서 신호를 확인하세요',stale:'신호가 다시 들어오면 조작이 이어집니다',ready:'폰을 똑바로 세우고 수직보정 버튼을 누르세요',active:sensorSource==='motion'?(motionHasGyro?'동작 센서로 조작 중 · 회전이 밀리면 수직보정':'회전 센서 신호 없음 · 기울기만 조작됩니다'):'폰을 움직이면 아래 각도와 방향이 바뀝니다'}[state]||sensorError;
  $('#gyro-panel').dataset.state=state;
  $('#gyro-title').textContent=title;
  $('#sensor-state').textContent=detail;
  $('#play').classList.toggle('gyro-active',gyroActive);
  $('#sensor').classList.toggle('active',gyroActive);
  $('#sensor').hidden=sensorRequesting||(gyroWanted&&!sensorError&&state!=='stale');
  $('#sensor').textContent=gyroWanted?'센서 다시 연결':'자이로 켜기';
  $('#recenter').hidden=!gyroWanted;
  $('#touch-switch').hidden=!gyroWanted;
  $('#recenter').disabled=!gyroWanted||!fresh;
  $('#sensor-readout').hidden=!gyroWanted&&!sensorError;
  $('#sensor-angle').textContent=gyroWanted?`${sensorSource==='motion'?'동작':'방향'} 센서 입력 ${sensorSamples}회${fresh?' · 실시간 수신 중':''}`:'';
  $('#gyro-feedback').hidden=!gyroActive;
  if(gyroActive){
    const degrees=value=>Math.round(Math.asin(Math.min(1,Math.abs(value)))*180/Math.PI);
    const lean=Math.round(Math.asin(Math.min(1,Math.hypot(tilt.x,tilt.z)))*180/Math.PI);
    $('#tilt-total').textContent=`${lean}°`;
    $('#tilt-front').textContent=tilt.z>.015?`앞 ${degrees(tilt.z)}°`:tilt.z<-.015?`뒤 ${degrees(tilt.z)}°`:'수직 0°';
    $('#tilt-side').textContent=tilt.x>.015?`오른쪽 ${degrees(tilt.x)}°`:tilt.x<-.015?`왼쪽 ${degrees(tilt.x)}°`:'중앙 0°';
    $('#tilt-yaw').textContent=sensorSource==='motion'&&!motionHasGyro?'측정 불가':`${Math.round(yaw)}°`;
    $('#tilt-dot').style.transform=`translate(${Math.round(tilt.x*38)}px,${Math.round(-tilt.z*38)}px)`;
  }
}
function stopPose(){if(poseSent&&joined)transmit({type:'pose',active:false});poseSent=false;}
function release(){pointers.clear();document.querySelectorAll('.pressed').forEach(button=>button.classList.remove('pressed'));if(joined)transmit({type:'input',bits:0});stopPose();}
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
  sensorSamples++;
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
  lastSensor=now;sensorSamples++;sensorError='';
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
    if(results.length&&results.every(result=>result.status!=='fulfilled'||result.value!=='granted'))throw Error(iphone?'동작 및 방향 접근을 허용하세요 · Safari에서 다시 시도':'브라우저에서 동작/방향 권한을 허용해 주세요');
    gyroWanted=true;gyroActive=false;reference=null;sensorError='';focused=true;
    const request=++sensorRequestId;
    setTimeout(()=>{if(request===sensorRequestId&&gyroWanted&&!sensorFresh()){sensorError=missingSensorText();gyroUI();}},3000);
    try{wake=await navigator.wakeLock?.request('screen');}catch{}
  }catch(error){sensorError=error.message||'센서 권한 오류';}
  finally{sensorRequesting=false;gyroUI();}
};
$('#recenter').onclick=()=>{stopPose();calibrate();};
$('#touch-switch').onclick=()=>{stopPose();gyroActive=false;gyroWanted=false;reference=null;sensorError='';sensorSamples=0;sensorRequestId++;wake?.release();wake=null;gyroUI();};
gyroUI();
function showVisual(visual){
  currentMode=['normal','giraffe','tennis','ice','squid'].includes(visual?.mode)?visual.mode:'normal';
  colorUI();
  const power={giraffe:2,tennis:3,ice:4,squid:5}[currentMode];
  document.querySelectorAll('[data-power]').forEach(button=>button.classList.toggle('active',Number(button.dataset.power)===power));
}
function showSlot(newSlot){
  slot=newSlot;$('#slot-name').textContent=labels[slot];
  document.querySelectorAll('[data-leg]').forEach(button=>button.classList.toggle('mine',Number(button.dataset.leg)===slot));
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
  ws=new WebSocket(`${location.protocol==='https:'?'wss:':'ws:'}//${location.host}/ws`);
  ws.onopen=()=>transmit({type:'hello',role:'player',code});
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
    const bit=Number(button.dataset.bit);if(!joined||!unity||(gyroActive&&bit<16))return;
    event.preventDefault();button.setPointerCapture(event.pointerId);pointers.set(event.pointerId,bit);
    button.classList.add('pressed');transmit({type:'input',bits:bits()});navigator.vibrate?.(8);
  });
  const up=event=>{pointers.delete(event.pointerId);if(![...pointers.values()].includes(Number(button.dataset.bit)))button.classList.remove('pressed');if(joined)transmit({type:'input',bits:bits()});};
  for(const name of ['pointerup','pointercancel','lostpointercapture'])button.addEventListener(name,up);
}
for(const button of document.querySelectorAll('[data-power]'))button.onclick=()=>{if(joined&&unity)transmit({type:'power',power:Number(button.dataset.power)});};
window.addEventListener('blur',()=>{focused=false;release();});
window.addEventListener('focus',()=>{focused=true;});
window.addEventListener('pagehide',release);
document.addEventListener('visibilitychange',()=>{if(document.hidden)release();});
setInterval(()=>{if(joined)transmit({type:'input',bits:bits()});},100);
setInterval(()=>{
  if(joined&&unity&&gyroActive&&document.visibilityState==='visible'&&sensorFresh()){transmit({type:'pose',active:true,x:tilt.x,z:tilt.z,yaw});poseSent=true;}
  else stopPose();
  if(gyroWanted&&!sensorFresh())gyroUI();
},40);
$('#leave').onclick=()=>{wanted=false;clearTimeout(retry);release();ws?.close();wake?.release();wake=null;gyroActive=false;gyroWanted=false;reference=null;sensorError='';sensorSamples=0;sensorRequestId++;gyroUI();$('#play').hidden=true;$('#entry').hidden=false;history.replaceState(null,'',location.pathname+location.search);message('');};
const inviteCode=clean(new URLSearchParams(location.hash.slice(1)).get('code'));
if(inviteCode.length===8){$('#room-code').value=format(inviteCode);wanted=true;connect();}
