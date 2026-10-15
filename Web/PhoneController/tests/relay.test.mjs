import {test} from 'node:test';
import assert from 'node:assert/strict';
import {once} from 'node:events';
import {WebSocket} from 'ws';
import {createRelay} from '../server.mjs';

async function socket(port,room,role,key,name='test'){
  const ws=new WebSocket(`ws://127.0.0.1:${port}/ws`),messages=[];
  ws.on('message',b=>messages.push(JSON.parse(b)));
  await once(ws,'open');ws.send(JSON.stringify({type:'hello',role,room,...(['player','viewer'].includes(role)?{code:key}:{key}),name}));
  return {ws,messages,send:m=>ws.send(JSON.stringify(m))};
}
async function until(fn,ms=2500){const end=Date.now()+ms;while(Date.now()<end){const v=fn();if(v)return v;await new Promise(r=>setTimeout(r,10));}throw Error('Timed out');}
const latest=(c,slot)=>c.messages.filter(m=>m.type==='input'&&m.slot===slot).at(-1);
const latestPose=(c,slot)=>c.messages.filter(m=>m.type==='pose'&&m.slot===slot).at(-1);

test('4 phones → authenticated relay → Unity: isolation, timeout, disconnect, reconnect',async()=>{
  const relay=await createRelay({port:0}),{room,port}=relay;
  try{
    const unity=await socket(port,room.id,'unity',room.hostKey);
    await until(()=>unity.messages.some(m=>m.type==='ready'));
    const phones=[];
    for(let i=0;i<4;i++){
      const p=await socket(port,room.id,'player',room.codes[i],`phone${i}`);phones.push(p);
      assert.equal((await until(()=>p.messages.find(m=>m.type==='joined'))).slot,i);
      assert.equal(p.messages.some(m=>m.codes),false);
    }
    const fifth=await socket(port,room.id,'player',room.codes[0]);
    const [code]=await once(fifth.ws,'close');assert.equal(code,1008);
    const badCode=await socket(port,room.id,'player','AAAAAAAA');assert.equal((await once(badCode.ws,'close'))[0],1008);
    const bad=await socket(port,room.id,'unity','bad-key');assert.equal((await once(bad.ws,'close'))[0],1008);
    const duplicate=await socket(port,room.id,'unity',room.hostKey);assert.equal((await once(duplicate.ws,'close'))[0],1008);
    for(let i=0;i<4;i++)phones[i].send({type:'input',slot:(i+1)%4,bits:1<<i});
    for(let i=0;i<4;i++)await until(()=>latest(unity,i)?.bits===(1<<i));
    // Every socket controls only its server-assigned leg, even if it forges slot.
    assert.deepEqual([0,1,2,3].map(i=>latest(unity,i).bits),[1,2,4,8]);
    phones[0].send({type:'pose',slot:3,active:true,x:.3,z:.4,yaw:45});
    await until(()=>latestPose(unity,0)?.active===true);
    assert.deepEqual([latestPose(unity,0).x,latestPose(unity,0).z],[.3,.4]);
    assert.equal(latestPose(unity,0).yaw,45);
    assert.equal(latestPose(unity,3)?.active,false);
    phones[0].send({type:'pose',active:false});await until(()=>latestPose(unity,0)?.active===false);
    phones[1].send({type:'pose',active:true,x:0,z:.8,yaw:0});await until(()=>latestPose(unity,1)?.active===true);
    await until(()=>latestPose(unity,1)?.active===false,1500);
    phones[0].send({type:'input',bits:80});await until(()=>latest(unity,0)?.bits===80);
    phones[0].send({type:'input',bits:0});await until(()=>latest(unity,0)?.bits===0);
    phones[0].send({type:'power',slot:3,power:2});assert.equal((await until(()=>unity.messages.find(m=>m.type==='power'))).slot,0);
    phones[1].send({type:'input',bits:255});await until(()=>latest(unity,1)?.bits===255);
    await until(()=>latest(unity,1)?.bits===0,1500); // stale heartbeat release
    phones[2].send({type:'input',bits:80});await until(()=>latest(unity,2)?.bits===80);
    phones[2].ws.close();await until(()=>latest(unity,2)?.occupied===false&&latest(unity,2)?.bits===0);
    phones[0].send({type:'switch',slot:2});
    assert.equal((await until(()=>phones[0].messages.find(m=>m.type==='switched'))).slot,2);
    await until(()=>latest(unity,0)?.occupied===false&&latest(unity,2)?.occupied===true);
    phones[0].send({type:'input',bits:4});await until(()=>latest(unity,2)?.bits===4);
    assert.equal(latest(unity,0)?.bits,0);
    phones[0].send({type:'switch',slot:1});
    await until(()=>phones[0].messages.some(m=>m.type==='error'&&m.message==='사용 중인 다리'));
    phones[0].send({type:'switch',slot:0});await until(()=>phones[0].messages.filter(m=>m.type==='switched').at(-1)?.slot===0);
    const replacement=await socket(port,room.id,'player',room.codes[2]);assert.equal((await until(()=>replacement.messages.find(m=>m.type==='joined'))).slot,2);
    phones[3].send({type:'ping',sent:123});assert.equal((await until(()=>phones[3].messages.find(m=>m.type==='pong'))).sent,123);
    unity.ws.close();await until(()=>phones[0].messages.filter(m=>m.type==='status').at(-1)?.unity===false);
    const nextUnity=await socket(port,room.id,'unity',room.hostKey);await until(()=>nextUnity.messages.some(m=>m.type==='ready'));
    const lobby=await until(()=>nextUnity.messages.find(m=>m.type==='lobby'));
    assert.deepEqual(lobby.codes,room.codes);
    assert.deepEqual(lobby.occupiedSlots,[true,true,true,true]);
    const viewer=await socket(port,room.id,'viewer',room.watchCode);
    await until(()=>viewer.messages.some(m=>m.type==='watch-ready'));
    await until(()=>nextUnity.messages.filter(m=>m.type==='lobby').at(-1)?.viewers===1);
    const fakeJpeg=Buffer.from([255,216,255,217]).toString('base64');
    nextUnity.send({type:'frame',data:fakeJpeg});
    assert.equal((await until(()=>viewer.messages.find(m=>m.type==='frame'))).data,fakeJpeg);
    assert.equal(phones[0].messages.some(m=>m.type==='frame'),false);
    nextUnity.send({type:'visual',mode:'giraffe'});
    await until(()=>phones[0].messages.filter(m=>m.type==='status').at(-1)?.visual?.mode==='giraffe');
    assert.equal(latest(nextUnity,0)?.bits,0);
    assert.equal((await fetch(`http://127.0.0.1:${port}/api/session`,{headers:{'cf-connecting-ip':'8.8.8.8'}})).status,403);
    assert.equal((await fetch(`http://127.0.0.1:${port}/api/session`,{headers:{'x-forwarded-for':'8.8.8.8'}})).status,403);
    for(const path of ['/','/host','/watch','/controller.mjs','/orientation.mjs','/host.mjs','/watch.mjs','/style.css','/health'])assert.equal((await fetch(`http://127.0.0.1:${port}${path}`)).status,200);
    assert.equal((await fetch(`http://127.0.0.1:${port}/PhoneController.json`)).status,404);
    const qr=await fetch(`http://127.0.0.1:${port}/api/qr?text=https%3A%2F%2Fexample.com`);assert.equal(qr.status,200);assert.match(await qr.text(),/<svg/);
    const png=await fetch(`http://127.0.0.1:${port}/api/qr?format=png&text=https%3A%2F%2Fexample.com`);assert.equal(png.status,200);assert.deepEqual([...new Uint8Array(await png.arrayBuffer()).slice(0,8)],[137,80,78,71,13,10,26,10]);
    for(const mode of ['normal','giraffe','tennis','ice','squid'])for(const suffix of ['','-colors'])assert.equal((await fetch(`http://127.0.0.1:${port}/assets/table-${mode}${suffix}.png`)).status,200);
    assert.equal((await fetch(`http://127.0.0.1:${port}/api/public-origin`,{method:'POST',body:JSON.stringify({origin:'http://unsafe.test'})})).status,400);
    assert.equal((await fetch(`http://127.0.0.1:${port}/api/public-origin`,{method:'POST',body:JSON.stringify({origin:'https://example.com'})})).status,200);
    const qrLobby=await until(()=>nextUnity.messages.filter(m=>m.type==='lobby').at(-1)?.qrPng?.length===4&&nextUnity.messages.filter(m=>m.type==='lobby').at(-1));
    assert.equal(qrLobby.qrPng.length,4);
    assert.deepEqual([...Buffer.from(qrLobby.qrPng[0],'base64').subarray(0,8)],[137,80,78,71,13,10,26,10]);
  }finally{await relay.close();}
});

test('native PC legs and phone legs share one room without double ownership',async()=>{
  const relay=await createRelay({port:0}),{room,port}=relay;
  try{
    const unity=await socket(port,room.id,'unity',room.hostKey);
    await until(()=>unity.messages.some(m=>m.type==='ready'));
    unity.send({type:'native-room',code:'ABC12'});
    await until(()=>unity.messages.filter(m=>m.type==='lobby').at(-1)?.nativeRoomCode==='ABC12');
    unity.send({type:'native-slots',bits:5});
    await until(()=>unity.messages.filter(m=>m.type==='lobby').at(-1)?.nativeSlots?.[2]===true);
    const reserved=await socket(port,room.id,'player',room.codes[0]);
    assert.equal((await once(reserved.ws,'close'))[0],1008);
    const phone=await socket(port,room.id,'player',room.codes[1]);
    await until(()=>phone.messages.some(m=>m.type==='joined'));
    phone.send({type:'switch',slot:2});
    await until(()=>phone.messages.some(m=>m.type==='error'&&m.message==='사용 중인 다리'));
    phone.send({type:'switch',slot:3});
    await until(()=>phone.messages.some(m=>m.type==='switched'&&m.slot===3));
    unity.send({type:'native-slots',bits:13});
    assert.equal((await once(phone.ws,'close'))[0],1008);
    await until(()=>latest(unity,3)?.occupied===false);
    unity.send({type:'native-slots',bits:0});
    await until(()=>unity.messages.filter(m=>m.type==='lobby').at(-1)?.nativeSlots?.every(v=>!v));
    const reopened=await socket(port,room.id,'player',room.codes[0]);
    assert.equal((await until(()=>reopened.messages.find(m=>m.type==='joined'))).slot,0);
  }finally{await relay.close();}
});
