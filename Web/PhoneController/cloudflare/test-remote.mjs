import assert from 'node:assert/strict';
import {WebSocket} from 'ws';
import {once} from 'node:events';
import {writeFile,mkdir} from 'node:fs/promises';
const origin=process.env.WAT_CONTROLLER_ORIGIN;
assert.ok(origin?.startsWith('https://'),'Set WAT_CONTROLLER_ORIGIN to the deployed service.');
const clients=[],checks=[];
const wait=ms=>new Promise(r=>setTimeout(r,ms));
async function until(f,label){for(let i=0;i<200;i++){const value=f();if(value)return value;await wait(25);}throw Error('Timeout: '+label);}
async function create(){const r=await fetch(origin+'/api/rooms',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});assert.equal(r.status,201,'room creation status');return r.json();}
async function connect(room,role,extra={}){
 const value=role==='viewer'?room.watchCode:room.joinCode;
 const ws=new WebSocket(origin.replace('https:','wss:')+'/ws?code='+value,{origin});
 const c={ws,messages:[],closed:null};clients.push(c);
 ws.on('message',raw=>c.messages.push(JSON.parse(raw)));
 ws.on('close',code=>c.closed=code);await once(ws,'open');
 ws.send(JSON.stringify({type:'hello',role,...(role==='unity'||role==='host'?{room:room.room,key:room.key}:{code:value}),...extra}));
 return c;
}
const send=(c,value)=>c.ws.send(JSON.stringify(value));
async function check(name,f){await f();checks.push({name,pass:true});console.log('PASS '+name);}
let a,b;
try{
 await check('public service works and private local APIs remain unavailable',async()=>{
  assert.equal((await fetch(origin+'/health')).status,200);
  assert.equal((await fetch(origin+'/api/session')).status,403);
  assert.equal((await fetch(origin+'/api/rooms',{method:'POST',headers:{Origin:'https://unrelated.example'}})).status,403);
  const html=await(await fetch(origin+'/')).text();assert.ok(html.includes('ability-actions'));
  a=await create();b=await create();assert.notEqual(a.room,b.room);assert.notEqual(a.key,b.key);
 });
 await check('invalid room code closes with a user-visible policy error',async()=>{
  const ws=new WebSocket(origin.replace('https:','wss:')+'/ws?code=00000000',{origin});clients.push({ws});
  const [code]=await once(ws,'close');assert.equal(code,1008);
 });
 const unity=await connect(a,'unity');await until(()=>unity.messages.some(m=>m.type==='ready'),'Unity auth');
 await check('native host receives one valid PNG QR and rejects another host',async()=>{
  const lobby=await until(()=>unity.messages.find(m=>m.type==='lobby'),'lobby');
  assert.equal(lobby.joinCode,a.joinCode);assert.equal(lobby.origin,origin);
  assert.deepEqual([...Buffer.from(lobby.joinQrPng,'base64').subarray(0,8)],[137,80,78,71,13,10,26,10]);
  const duplicate=await connect(a,'unity');await until(()=>duplicate.closed===1008,'duplicate host blocked');
 });
 const phones=[];
 await check('one invite assigns four unique legs and rejects a fifth phone',async()=>{
  for(let i=0;i<4;i++){const p=await connect(a,'player');phones.push(p);const m=await until(()=>p.messages.find(m=>m.type==='joined'),'phone '+i);assert.equal(m.slot,i);}
  const fifth=await connect(a,'player');await until(()=>fifth.closed===1008,'room full');
 });
 await check('two simultaneous rooms keep their input streams separate',async()=>{
  const other=await connect(b,'unity');await until(()=>other.messages.some(m=>m.type==='ready'),'second host');
  const phone=await connect(b,'player');await until(()=>phone.messages.some(m=>m.type==='joined'),'second room phone');
  unity.messages.length=0;send(phone,{type:'input',bits:8,mode:'normal'});
  await until(()=>other.messages.some(m=>m.type==='input'&&m.bits===8),'second-room direction');
  assert.equal(unity.messages.some(m=>m.type==='input'&&m.bits===8),false);
 });
 await check('actions follow the host power and stale actions are dropped',async()=>{
  send(unity,{type:'visual',mode:'giraffe'});await until(()=>phones[0].messages.some(m=>m.visual?.mode==='giraffe'),'giraffe state');
  unity.messages.length=0;send(phones[0],{type:'input',bits:16,mode:'giraffe'});
  await until(()=>unity.messages.some(m=>m.type==='input'&&m.slot===0&&m.bits===16),'length action');
  send(unity,{type:'visual',mode:'ice',iceFrozen:false});await until(()=>phones[0].messages.some(m=>m.visual?.mode==='ice'&&m.visual.iceFrozen===false),'ice state');
  unity.messages.length=0;send(phones[0],{type:'input',bits:17,mode:'giraffe'});
  await until(()=>unity.messages.some(m=>m.type==='input'&&m.slot===0&&m.bits===1),'stale action mask');
  send(phones[0],{type:'power',power:5});await wait(100);assert.equal(unity.messages.some(m=>m.type==='power'),false);
 });
 await check('calibrated pose and watchdog release travel through the cloud',async()=>{
  unity.messages.length=0;send(phones[0],{type:'pose',active:true,x:.3,z:.4,yaw:25});
  await until(()=>unity.messages.some(m=>m.type==='pose'&&m.active&&m.x===.3),'pose');
  await until(()=>unity.messages.some(m=>m.type==='pose'&&!m.active),'pose timeout');
  unity.messages.length=0;send(phones[0],{type:'input',bits:2,mode:'ice'});
  await until(()=>unity.messages.some(m=>m.type==='input'&&m.bits===2),'held key');
  await until(()=>unity.messages.some(m=>m.type==='input'&&m.slot===0&&m.bits===0),'input timeout');
 });
 await check('PC leg reservations evict conflicting phones and prevent switching',async()=>{
  send(unity,{type:'native-slots',bits:1});await until(()=>phones[0].closed===1008,'PC reservation');
  send(phones[1],{type:'switch',slot:0});await until(()=>phones[1].messages.some(m=>m.type==='error'),'occupied switch');
 });
 await check('watch code is separate and viewer receives host frames',async()=>{
  const viewer=await connect(a,'viewer');await until(()=>viewer.messages.some(m=>m.type==='watch-ready'),'viewer auth');
  const data=Buffer.from('protocol-frame').toString('base64');send(unity,{type:'frame',data});
  await until(()=>viewer.messages.some(m=>m.type==='frame'&&m.data===data),'frame forwarding');
  const next=Buffer.from('protocol-next-frame').toString('base64');
  send(unity,{type:'frame',data:next});await wait(150);
  assert.equal(viewer.messages.some(m=>m.type==='frame'&&m.data===next),false,'slow viewer queue must stay bounded');
  send(viewer,{type:'frame-ack'});await wait(100);send(unity,{type:'frame',data:next});
  await until(()=>viewer.messages.some(m=>m.type==='frame'&&m.data===next),'render acknowledgment');
 });
 await check('host can reconnect to its room after disconnection',async()=>{
  unity.ws.close();await until(()=>unity.closed!==null,'host disconnect');
  const next=await connect(a,'unity');await until(()=>next.messages.some(m=>m.type==='ready'),'host reconnect');
 });
}finally{
 for(const c of clients)c.ws.close();
 const result={origin,transport:'public HTTPS + WSS on Cloudflare Durable Objects',checks,passed:checks.length};
 const folder=process.env.WAT_TEST_OUTPUT;if(folder){await mkdir(folder,{recursive:true});await writeFile(folder+'/cloud-protocol.json',JSON.stringify(result,null,2));}
 console.log(JSON.stringify({passed:checks.length,origin}));
}
