// Requires a running relay, public tunnel, and native Unity player.
import assert from 'node:assert/strict';
import {WebSocket} from 'ws';

const session=await (await fetch('http://127.0.0.1:4180/api/session')).json();
assert.ok(session.origin?.startsWith('https://'));
assert.ok(session.unity,'Unity player is not connected');
const endpoint=session.origin.replace(/^https:/,'wss:')+'/ws';
const peers=[];
function join(role,code){
  return new Promise((resolve,reject)=>{
    const ws=new WebSocket(endpoint);
    const timer=setTimeout(()=>reject(Error(`${role} timed out`)),15000);
    ws.on('open',()=>ws.send(JSON.stringify({type:'hello',role,code})));
    ws.on('message',buffer=>{
      const message=JSON.parse(buffer);
      if(message.type==='watch-ready'||message.type==='joined'){
        clearTimeout(timer);peers.push(ws);resolve({ws,message});
      }
    });
    ws.on('error',reject);
  });
}
try{
  const viewerFrames=[];
  for(let i=0;i<4;i++){
    const viewer=await join('viewer',session.watchCode),frames=[];
    viewerFrames.push(frames);
    viewer.ws.on('message',buffer=>{
      const message=JSON.parse(buffer);
      if(message.type==='frame')frames.push({time:Date.now(),size:Buffer.from(message.data,'base64').length,data:message.data});
    });
  }
  const phones=[];
  for(let i=0;i<4;i++)phones.push(await join('player',session.codes[i]));
  assert.deepEqual(phones.map(p=>p.message.slot),[0,1,2,3]);
  const until=Date.now()+15000;
  while(viewerFrames.some(f=>f.length<8)&&Date.now()<until)await new Promise(r=>setTimeout(r,100));
  assert.ok(viewerFrames.every(f=>f.length>=8),`Frames per viewer: ${viewerFrames.map(f=>f.length)}`);
  assert.ok(viewerFrames.every(frames=>frames.every(f=>f.data.startsWith('/9j/')&&f.size>1000)));
  // A common captured frame must arrive at every PC, not separate simulations.
  const common=viewerFrames[0].find(frame=>viewerFrames.every(frames=>frames.some(f=>f.data===frame.data)));
  assert.ok(common,'No identical frame reached all public viewers');
  console.log(JSON.stringify({origin:session.origin,phones:phones.length,viewers:viewerFrames.length,framesPerViewer:viewerFrames.map(f=>f.length),firstToEighthMs:viewerFrames[0][7].time-viewerFrames[0][0].time,bytes:viewerFrames[0].slice(0,8).map(f=>f.size)}));
}finally{for(const ws of peers)ws.close();}
