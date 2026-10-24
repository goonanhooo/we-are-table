// Run against the live local relay + its public tunnel. Does not print either room key.
import {WebSocket} from 'ws';
import {once} from 'node:events';
import assert from 'node:assert/strict';
const s=await(await fetch('http://127.0.0.1:4180/api/session')).json();
assert.ok(s.origin);
assert.equal((await fetch(s.origin+'/health')).status,200);
assert.equal((await fetch(s.origin+'/api/session')).status,403);
assert.equal((await fetch('http://127.0.0.1:4180/api/session',{headers:{Origin:'https://attacker.example'}})).status,403);
const until=async f=>{for(let i=0;i<250;i++){const v=f();if(v)return v;await new Promise(r=>setTimeout(r,20));}throw Error('timeout');};
async function client(url,role,key){
 const ws=new WebSocket(url,role==='player'?{origin:s.origin}:{}),messages=[];ws.on('message',m=>messages.push(JSON.parse(m)));await once(ws,'open');ws.send(JSON.stringify({type:'hello',role,room:s.room,...(role==='player'?{code:key}:{key}),name:'internet-test'}));return {ws,messages};
}
let unity;const phones=[];
try{
 unity=await client('ws://127.0.0.1:4180/ws','unity',s.hostKey);
 await until(()=>unity.messages.some(m=>m.type==='ready'));
 for(let i=0;i<4;i++){
  const phone=await client(s.origin.replace('https:','wss:')+'/ws','player',s.joinCode);phones.push(phone);
  assert.equal((await until(()=>phone.messages.find(m=>m.type==='joined'))).slot,i);
 }
 const phone=phones[0],joined={slot:0};
 for(let i=0;i<4;i++)phones[i].ws.send(JSON.stringify({type:'input',bits:1<<i}));
 for(let i=0;i<4;i++)await until(()=>unity.messages.some(m=>m.type==='input'&&m.slot===i&&m.bits===1<<i));
 for(const bits of [1,4,80,160,0]){
  unity.messages.length=0;phone.ws.send(JSON.stringify({type:'input',bits}));
  await until(()=>unity.messages.some(m=>m.type==='input'&&m.slot===joined.slot&&m.bits===bits));
 }
 unity.messages.length=0;phone.ws.send(JSON.stringify({type:'pose',active:true,x:.3,z:.4,yaw:0}));
 await until(()=>unity.messages.some(m=>m.type==='pose'&&m.slot===joined.slot&&m.active&&m.x===.3&&m.z===.4));
 phone.ws.send(JSON.stringify({type:'pose',active:false}));
 await until(()=>unity.messages.some(m=>m.type==='pose'&&m.slot===joined.slot&&!m.active));
 phone.ws.send(JSON.stringify({type:'power',power:3}));
 phone.ws.send(JSON.stringify({type:'input',bits:160}));
 await until(()=>unity.messages.some(m=>m.type==='input'&&m.slot===joined.slot&&m.bits===160));
 assert.equal(unity.messages.some(m=>m.type==='power'),false);
 const timings=[];
 for(let i=0;i<5;i++){
  const sent=performance.now();phone.ws.send(JSON.stringify({type:'ping',sent}));
  await until(()=>phone.messages.some(m=>m.type==='pong'&&m.sent===sent));timings.push(Math.round(performance.now()-sent));
 }
 console.log(JSON.stringify({publicHttps:true,publicWss:true,oneInviteFourSimultaneousLegs:true,publicHostCredentialsBlocked:true,forgedOriginBlocked:true,inputMasksVerified:[1,4,80,160,0],gyroPoseVerified:true,phoneAbilityCommandsBlocked:true,roundTripMs:timings,note:'Public tunnel loop from this PC; not a two-location human playtest.'},null,2));
}finally{for(const phone of phones)phone.ws.close();unity?.ws.close();}
