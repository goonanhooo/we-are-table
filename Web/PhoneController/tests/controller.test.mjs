import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
import {orientationQuaternion,gravityRelativeQuaternion,calibratedLegTilt,calibratedLegYaw} from '../public/orientation.mjs';

test('upright calibration and left/right orientation',()=>{
  const upright=orientationQuaternion(0,90,0);
  assert.deepEqual(calibratedLegTilt(upright,upright),{x:0,z:0});
  assert.ok(Math.abs(calibratedLegTilt(upright,orientationQuaternion(0,120,0)).z-.5)<1e-6);
  const straight=orientationQuaternion(0,0,0);
  const right=calibratedLegTilt(straight,orientationQuaternion(30,0,0));
  const left=calibratedLegTilt(straight,orientationQuaternion(-30,0,0));
  assert.ok(right.x*left.x<0);
  assert.ok(Math.abs(right.x+left.x)<1e-6);
  assert.ok(Math.abs(calibratedLegYaw(upright,orientationQuaternion(30,90,0))-30)<1e-6);
  const gravity=gravityRelativeQuaternion({x:0,y:9.8,z:0},{x:0,y:8.487,z:-4.9});
  assert.ok(Math.abs(calibratedLegTilt({w:1,x:0,y:0,z:0},gravity).z-.5)<.001);
});

test('shared invite, leg colors, nickname, keys / gyro / joystick modes',async()=>{
  function element(dataset={}){
    const events={},classes=new Set();
    const style={setProperty(name,value){this[name]=value;}};
    return {dataset,events,style,hidden:false,value:'',textContent:'',src:'',getBoundingClientRect:()=>({left:0,top:0,width:200,height:200}),classList:{add:c=>classes.add(c),remove:c=>classes.delete(c),toggle:(c,b)=>{if(b===undefined)b=!classes.has(c);b?classes.add(c):classes.delete(c);},contains:c=>classes.has(c)},addEventListener:(n,f)=>events[n]=f,setPointerCapture(){},setAttribute(){},removeAttribute(){}};
  }
  const ids=new Map();for(const id of ['message','connection','game-state','entry','play','slot-name','table-image','leg-key','color-toggle','gyro-controls','gyro-title','pad','tilt-dot','mini-dot','sensor','recenter','sensor-state','room-code','join-form','leave','nickname'])ids.set('#'+id,element());
  const buttons=[1,2,4,8,0,0].map(bit=>element({bit:String(bit)}));
  ids.set('#action-primary',buttons[4]);ids.set('#action-secondary',buttons[5]);
  for(const id of ['ability-actions','action-primary-symbol','action-primary-label','action-secondary-symbol','action-secondary-label'])ids.set('#'+id,element());
  const marks=[0,1,2,3].map(leg=>element({leg:String(leg)}));
  const powers=[2,3,4,5].map(power=>element({power:String(power)}));
  const modes=['keys','gyro','stick'].map(mode=>element({mode}));
  const dirs=[1,4,2,8].map(dir=>element({dir:String(dir)}));
  const events={},timers=[],permissionCalls=[];let now=100;
  const document={visibilityState:'visible',hidden:false,querySelector:s=>ids.get(s),querySelectorAll:s=>s==='[data-bit]'?buttons:s==='[data-leg]'?marks:s==='[data-power]'?powers:s==='#modes [data-mode]'?modes:s==='[data-dir]'?dirs:s==='.pressed'?buttons.filter(b=>b.classList.contains('pressed')):[],addEventListener:(n,f)=>events[n]=f};
  const window={addEventListener:(n,f)=>events[n]=f};
  class FakeSocket{
    static OPEN=1;static CONNECTING=0;static instance;
    constructor(){this.readyState=0;this.sent=[];FakeSocket.instance=this;}
    send(s){this.sent.push(JSON.parse(s));}
    close(){this.readyState=3;this.onclose({code:1000});}
    receive(m){this.onmessage({data:JSON.stringify(m)});}
  }
  const context={document,window,WebSocket:FakeSocket,DeviceOrientationEvent:{requestPermission:()=>{permissionCalls.push('orientation');return Promise.resolve('granted');}},DeviceMotionEvent:{requestPermission:()=>{permissionCalls.push('motion');return Promise.resolve('granted');}},orientationQuaternion,gravityRelativeQuaternion,calibratedLegTilt,calibratedLegYaw,location:{protocol:'https:',host:'example.com',hash:'#code=ABCDEFGH',pathname:'/'},URLSearchParams,navigator:{vibrate(){},userAgent:'iPhone'},localStorage:{getItem:()=>null,setItem(){}},history:{replaceState(){}},performance:{now:()=>now},setInterval:f=>timers.push(f),setTimeout:()=>1,clearTimeout(){}};
  const script=(await readFile(new URL('../public/controller.mjs',import.meta.url),'utf8')).replace(/^import .*\n/,'');
  vm.runInNewContext(script,context);
  const ws=FakeSocket.instance;ws.readyState=1;ws.onopen();
  assert.deepEqual(ws.sent.at(-1),{type:'hello',role:'player',code:'ABCDEFGH',preferredSlot:-1,name:''});
  assert.equal(ids.get('#play').dataset.mode,'keys');
  ws.receive({type:'joined',slot:2});ws.receive({type:'status',unity:true,visual:{mode:'normal'}});
  assert.equal(ids.get('#game-state').textContent,'정상 연결');
  assert.equal(ids.get('#slot-name').textContent,'초록 다리');
  assert.equal(ids.get('#play').style['--leg-rgb'],'60 175 92');
  assert.equal(marks[2].classList.contains('mine'),true);
  ws.receive({type:'status',unity:true,occupiedSlots:[false,false,true,false],visual:{mode:'normal'}});
  marks[0].onclick();assert.deepEqual(ws.sent.at(-1),{type:'switch',slot:0});
  ws.receive({type:'switched',slot:0});
  ws.receive({type:'status',unity:true,occupiedSlots:[true,false,true,false],visual:{mode:'normal'}});
  assert.equal(ids.get('#slot-name').textContent,'노란 다리');
  assert.equal(ids.get('#play').style['--leg-rgb'],'242 187 43');
  assert.equal(marks[2].disabled,true);
  const pointer=id=>({pointerId:id,preventDefault(){}});
  // 게임이 다리를 고르는 중: 큰 다리 버튼 화면, 조작 입력은 안 보냄
  ws.receive({type:'status',unity:true,occupiedSlots:[true,false,true,false],visual:{mode:'normal',legSelect:true}});
  assert.equal(ids.get('#play').classList.contains('choosing'),true);
  buttons[0].events.pointerdown(pointer(40));timers[0]();assert.equal(ws.sent.at(-1).bits,0);buttons[0].events.pointerup(pointer(40));
  ws.receive({type:'status',unity:true,occupiedSlots:[true,false,true,false],visual:{mode:'normal'}});
  assert.equal(ids.get('#play').classList.contains('choosing'),false);
  buttons[0].events.pointerdown(pointer(1));buttons[2].events.pointerdown(pointer(2));assert.equal(ws.sent.at(-1).bits,5);
  assert.equal(ids.get('#tilt-dot').style.transform,'translate(-25cqw,-25cqw)');
  assert.equal(ids.get('#mini-dot').style.transform,'translate(-25cqw,-25cqw)');
  assert.deepEqual(dirs.map(d=>d.classList.contains('lit')),[true,true,false,false]);
  timers[0]();assert.equal(ws.sent.at(-1).bits,5);
  buttons[0].events.pointerup(pointer(1));assert.equal(ws.sent.at(-1).bits,4);
  buttons[2].events.pointercancel(pointer(2));assert.equal(ws.sent.at(-1).bits,0);
  assert.equal(powers[0].onclick,undefined);
  ws.receive({type:'status',unity:true,visual:{mode:'giraffe'}});
  assert.equal(ids.get('#table-image').src,'/assets/table-giraffe-colors.png');
  ids.get('#color-toggle').onclick();assert.equal(ids.get('#table-image').src,'/assets/table-giraffe.png');assert.equal(ids.get('#leg-key').hidden,false);
  ws.receive({type:'status',unity:true,visual:{mode:'normal'}});
  assert.equal(ids.get('#table-image').src,'/assets/table-start.svg');
  assert.equal(ids.get('#ability-actions').hidden,true);
  const cases=[
    ['giraffe',['다리 늘이기','다리 줄이기'],[16,32]],
    ['tennis',['테니스공 접착','접착 해제'],[64,128]],
    ['ice',['얼음','풀기'],[16,32]],
    ['squid',['분출'],[16]],
  ];
  for(const [mode,names,expectedBits] of cases){
    ws.receive({type:'status',unity:true,visual:{mode,iceFrozen:true}});
    assert.equal(ids.get('#ability-actions').hidden,false);
    assert.equal(buttons[5].hidden,names.length===1);
    for(let i=0;i<names.length;i++){
      assert.equal(ids.get(i===0?'#action-primary-label':'#action-secondary-label').textContent,names[i]);
      buttons[i+4].events.pointerdown(pointer(10+i));
      assert.equal(ws.sent.at(-1).bits,expectedBits[i]);assert.equal(ws.sent.at(-1).mode,mode);
      buttons[i+4].events.pointerup(pointer(10+i));assert.equal(ws.sent.at(-1).bits,0);
    }
  }
  ws.receive({type:'status',unity:true,visual:{mode:'giraffe'}});
  buttons[4].events.pointerdown(pointer(20));assert.equal(ws.sent.at(-1).bits,16);
  ws.receive({type:'status',unity:true,visual:{mode:'tennis'}});
  assert.equal(ws.sent.at(-1).bits,0);assert.equal(ws.sent.at(-1).mode,'giraffe');
  timers[0]();assert.equal(ws.sent.at(-1).bits,0);assert.equal(ws.sent.at(-1).mode,'tennis');
  ws.receive({type:'status',unity:true,visual:{mode:'ice',iceFrozen:false}});
  assert.equal(ids.get('#ability-actions').hidden,false);
  assert.equal(ids.get('#action-secondary-label').textContent,'풀기');
  assert.equal(buttons[5].classList.contains('is-on'),true);
  assert.equal(ids.get('#table-image').src,'/assets/table-start.svg');
  ws.receive({type:'status',unity:true,visual:{mode:'ice',iceFrozen:true}});
  assert.equal(buttons[4].classList.contains('is-on'),true);
  assert.equal(ids.get('#table-image').src,'/assets/table-ice.png');
  ws.receive({type:'status',unity:true,visual:{mode:'normal'}});
  events.deviceorientation({alpha:0,beta:90,gamma:0});const enable=modes[1].onclick();
  assert.deepEqual(permissionCalls,['orientation','motion']);
  await enable;
  assert.equal(ids.get('#play').dataset.mode,'gyro');
  assert.equal(ids.get('#sensor').hidden,true);
  assert.equal(ids.get('#recenter').hidden,false);
  assert.equal(ids.get('#recenter').disabled,false);
  assert.equal(ids.get('#gyro-title').textContent,'자이로 켜짐, 수직보정 대기');
  assert.equal(ids.get('#sensor-state').hidden,true);
  timers[1]();assert.notEqual(ws.sent.at(-1).type,'pose');
  ids.get('#recenter').onclick();
  events.deviceorientation({alpha:0,beta:120,gamma:0});timers[1]();
  assert.equal(ws.sent.at(-1).type,'pose');assert.ok(Math.abs(ws.sent.at(-1).z-.5)<1e-6);
  assert.equal(ids.get('#gyro-title').textContent,'자이로 켜짐, 다리 조작 중');
  assert.equal(ids.get('#tilt-dot').style.transform,'translate(0cqw,-17cqw)');
  events.deviceorientation({alpha:30,beta:90,gamma:0});timers[1]();
  assert.ok(Math.abs(ws.sent.at(-1).yaw-30)<1e-6);
  ids.get('#recenter').onclick();timers[1]();assert.equal(ws.sent.at(-1).z,0);
  assert.equal(ids.get('#gyro-title').textContent,'자이로 켜짐, 다리 조작 중');
  now=1200;timers[1]();assert.deepEqual(ws.sent.at(-1),{type:'pose',active:false});
  assert.equal(ids.get('#gyro-title').textContent,'센서 신호 끊김');
  events.deviceorientation({alpha:30,beta:90,gamma:0});timers[1]();assert.equal(ws.sent.at(-1).active,true);
  modes[0].onclick();assert.equal(ids.get('#play').dataset.mode,'keys');assert.deepEqual(ws.sent.at(-1),{type:'pose',active:false});
  now=2500;
  events.devicemotion({accelerationIncludingGravity:{x:0,y:9.8,z:0},rotationRate:{beta:0}});
  await modes[1].onclick();
  assert.equal(ids.get('#sensor-state').hidden,true);
  ids.get('#recenter').onclick();
  now=2550;
  events.devicemotion({accelerationIncludingGravity:{x:0,y:8.487,z:-4.9},rotationRate:{beta:60}});timers[1]();
  assert.equal(ws.sent.at(-1).type,'pose');
  assert.ok(Math.abs(ws.sent.at(-1).z-.5)<.001);
  assert.ok(Math.abs(ws.sent.at(-1).yaw-3)<.001);
  buttons[1].events.pointerdown(pointer(3));ws.receive({type:'status',unity:false});
  assert.equal(ws.sent.findLast(item=>item.type==='input').bits,0);
  assert.equal(ids.get('#game-state').textContent,'게임 대기');
  // 조이스틱: 원을 끄는 만큼 pose, 가운데 근처와 손 뗀 뒤에는 멈춤, 방향키 비트는 안 보냄
  ws.receive({type:'status',unity:true,visual:{mode:'normal'}});
  modes[2].onclick();assert.equal(ids.get('#play').dataset.mode,'stick');
  const pad=ids.get('#pad'),touch=(x,y)=>({pointerId:7,clientX:x,clientY:y,preventDefault(){}});
  pad.events.pointerdown(touch(170,100));timers[1]();
  assert.deepEqual(ws.sent.at(-1),{type:'pose',active:true,x:1,z:0,yaw:0});
  pad.events.pointermove(touch(100,65));timers[1]();
  assert.ok(Math.abs(ws.sent.at(-1).x)<1e-9);assert.ok(Math.abs(ws.sent.at(-1).z-.5)<1e-9);
  assert.equal(ids.get('#tilt-dot').style.transform,'translate(0cqw,-17cqw)');
  pad.events.pointermove(touch(102,100));timers[1]();assert.deepEqual(ws.sent.at(-1),{type:'pose',active:false});
  pad.events.pointermove(touch(100,170));timers[1]();assert.equal(ws.sent.at(-1).z,-1);
  pad.events.pointerup(touch(100,170));assert.deepEqual(ws.sent.at(-1),{type:'pose',active:false});
  buttons[0].events.pointerdown(pointer(30));timers[0]();assert.equal(ws.sent.at(-1).bits,0);buttons[0].events.pointerup(pointer(30));
  // 닉네임: 다듬어서 보내고, 같은 이름은 다시 안 보냄
  ids.get('#nickname').value='  김 철수  ';ids.get('#nickname').events.change();
  assert.deepEqual(ws.sent.at(-1),{type:'name',name:'김 철수'});
  const count=ws.sent.length;ids.get('#nickname').events.change();assert.equal(ws.sent.length,count);
  ids.get('#leave').onclick();assert.equal(ids.get('#entry').hidden,false);assert.equal(ids.get('#play').dataset.mode,'keys');
  const html=await readFile(new URL('../public/index.html',import.meta.url),'utf8');
  assert.doesNotMatch(html,/data-power|sensor-readout|gyro-values|tilt-total|sensor-angle|touch-switch/);
  assert.match(html,/data-mode="keys"[^>]*>기본<\/button>\s*<button[^>]*data-mode="gyro"[^>]*>자이로<\/button>\s*<button[^>]*data-mode="stick"[^>]*>조이스틱/);
  assert.match(html,/id="nickname"/);
});
