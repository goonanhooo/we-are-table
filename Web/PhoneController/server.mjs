import http from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import {randomBytes,randomInt,timingSafeEqual} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {resolve,dirname} from 'node:path';
import {WebSocketServer,WebSocket} from 'ws';
import QRCode from 'qrcode';

const root=dirname(fileURLToPath(import.meta.url));
const secret=()=>randomBytes(24).toString('base64url');
const codeAlphabet='ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const createCode=()=>Array.from({length:8},()=>codeAlphabet[randomInt(codeAlphabet.length)]).join('');
const normalizeCode=value=>String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'');
const actionMasks={normal:0,giraffe:48,tennis:192,ice:48,squid:16};
const equal=(a,b)=>typeof a==='string' && a.length===b.length && timingSafeEqual(Buffer.from(a),Buffer.from(b));
const cleanName=value=>Array.from(String(value||'').replace(/[\x00-\x1f\x7f<>\u202a-\u202e\u2066-\u2069]/g,'').trim()).slice(0,16).join('');
const send=(ws,m)=>{if(ws?.readyState===WebSocket.OPEN && ws.bufferedAmount<65536)ws.send(JSON.stringify(m));};
const isLocal=req=>{
  if(!['127.0.0.1','::1','::ffff:127.0.0.1'].includes(req.socket.remoteAddress)||req.headers['cf-connecting-ip']||req.headers['x-forwarded-for'])return false;
  if(!/^(127\.0\.0\.1|localhost|\[::1\])(:\d+)?$/.test(req.headers.host||''))return false;
  if(req.headers.origin){try{if(new URL(req.headers.origin).host!==req.headers.host)return false;}catch{return false;}}
  return true;
};

export async function createRelay({port=4180,writeConfig=false}={}) {
  const codes=[];while(codes.length<4){const code=createCode();if(!codes.includes(code))codes.push(code);}
  let watchCode=createCode();while(codes.includes(watchCode))watchCode=createCode();
  let joinCode=createCode();while(codes.includes(joinCode)||joinCode===watchCode)joinCode=createCode();
  const room={id:randomBytes(5).toString('hex').toUpperCase(),hostKey:secret(),codes,joinCode,watchCode,nativeRoomCode:'',players:Array(4).fill(null),nativeSlots:[false,false,false,false],unity:null,observers:new Set(),viewers:new Set(),visual:{mode:'normal'},lastFrame:null};
  let publicOrigin='',joinQrPng='',closed=false;
  const status=()=>({type:'status',unity:!!room.unity,occupiedSlots:room.players.map((p,i)=>!!p||room.nativeSlots[i]),nativeSlots:room.nativeSlots,players:room.players.map(p=>p?{name:p.name,active:!!p.bits}:null),visual:room.visual});
  const lobby=()=>({type:'lobby',room:room.id,codes:room.codes,joinCode:room.joinCode,joinQrPng,watchCode:room.watchCode,nativeRoomCode:room.nativeRoomCode,origin:publicOrigin,occupiedSlots:room.players.map(Boolean),names:room.players.map(p=>p?p.name:''),nativeSlots:room.nativeSlots,unity:!!room.unity,viewers:room.viewers.size});
  const publish=()=>{const s=status();for(const p of room.players)if(p)send(p.ws,s);send(room.unity,lobby());for(const w of room.observers)send(w,lobby());for(const w of room.viewers)send(w,{type:'watch-status',unity:!!room.unity,viewers:room.viewers.size});};
  const input=(slot,bits)=>{const p=room.players[slot];if(p)p.bits=bits;send(room.unity,{type:'input',slot,bits,occupied:!!p});};
  const pose=(slot,active)=>{const p=room.players[slot];send(room.unity,{type:'pose',slot,active:!!p&&active,x:active?p?.x||0:0,z:active?p?.z||0:0,yaw:active?p?.yaw||0:0});};
  const snapshot=()=>{for(let i=0;i<4;i++){input(i,room.players[i]?.bits||0);const p=room.players[i];pose(i,!!p?.poseActive&&Date.now()-p.poseLast<=500);}};
  const headers={'Content-Type':'application/json','Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer'};
  const server=http.createServer(async(req,res)=>{
    try {
      const u=new URL(req.url,'http://local');
      if(u.pathname==='/health'){res.writeHead(200,headers);res.end(JSON.stringify({ok:true}));return;}
      if(u.pathname.startsWith('/api/')) {
        if(!isLocal(req)){res.writeHead(403,headers);res.end('{"error":"local host only"}');return;}
        if(u.pathname==='/api/session'){res.writeHead(200,headers);res.end(JSON.stringify({...lobby(),hostKey:room.hostKey,visual:room.visual}));return;}
        if(u.pathname==='/api/public-origin'&&req.method==='POST'){
          let body='';for await(const b of req){body+=b;if(body.length>2048)throw Error('body too large');}
          const origin=new URL(JSON.parse(body).origin);if(origin.protocol!=='https:'||origin.username||origin.password)throw Error('HTTPS origin required');
          publicOrigin=origin.origin;
          joinQrPng=(await QRCode.toBuffer(publicOrigin+'/#code='+room.joinCode,{type:'png',margin:2,width:320})).toString('base64');
          publish();res.writeHead(200,headers);res.end('{"ok":true}');return;
        }
        if(u.pathname==='/api/qr'){
          const value=u.searchParams.get('text')||'';if(value.length>2048)throw Error('too long');
          if(u.searchParams.get('format')==='png'){
            res.writeHead(200,{...headers,'Content-Type':'image/png'});res.end(await QRCode.toBuffer(value,{type:'png',margin:2,width:256}));
          }else{res.writeHead(200,{...headers,'Content-Type':'image/svg+xml'});res.end(await QRCode.toString(value,{type:'svg',margin:2,width:200}));}return;
        }
        res.writeHead(404,headers);res.end('{}');return;
      }
          const files={'/':'index.html','/host':'host.html','/watch':'watch.html','/controller.mjs':'controller.mjs','/orientation.mjs':'orientation.mjs','/host.mjs':'host.mjs','/watch.mjs':'watch.mjs','/style.css':'style.css','/preview':'preview.html','/preview.html':'preview.html','/preview.mjs':'preview.mjs','/preview.css':'preview.css','/preview-mock.mjs':'preview-mock.mjs'};
      const asset=/^\/assets\/(?:table-(?:normal|giraffe|tennis|ice|squid)(?:-colors)?\.png|table-start(?:-colors)?\.svg)$/.exec(u.pathname);
      const file=files[u.pathname]||(asset&&u.pathname.slice(1));if(!file){res.writeHead(404);res.end('Not found');return;}
      const mime=file.endsWith('.mjs')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.png')?'image/png':file.endsWith('.svg')?'image/svg+xml':'text/html';
      res.writeHead(200,{...headers,'Content-Type':mime+'; charset=utf-8','Content-Security-Policy':"default-src 'self'; connect-src 'self' ws: wss:; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'self'"});
      res.end(await readFile(resolve(root,'public',file)));
    }catch{if(!res.headersSent)res.writeHead(400,headers);res.end('{"error":"invalid request"}');}
  });
  const wss=new WebSocketServer({server,path:'/ws',maxPayload:262144,perMessageDeflate:false});
  wss.on('connection',(ws,req)=>{
    if(wss.clients.size>32){ws.close(1008,'capacity');return;}
    // Browsers must connect from the served origin. Unity has no Origin header.
    if(req.headers.origin){try{if(new URL(req.headers.origin).host!==req.headers.host){ws.close(1008,'origin');return;}}catch{ws.close(1008);return;}}
    let role='',slot=-1,windowStart=Date.now(),count=0;
    const authTimer=setTimeout(()=>ws.close(1008,'authentication required'),5000);
    ws.alive=true;ws.on('pong',()=>ws.alive=true);
    ws.on('message',raw=>{
      try{
        const now=Date.now();if(now-windowStart>1000){count=0;windowStart=now;}if(++count>100){ws.close(1008,'rate');return;}
        const m=JSON.parse(raw.toString());
        if(!role){
          if(m.type!=='hello'){ws.close(1008,'hello required');return;}
          if(m.role==='unity'&&m.room===room.id&&equal(m.key,room.hostKey)){
            if(room.unity){ws.close(1008,'host already connected');return;}
            role='unity';room.unity=ws;clearTimeout(authTimer);send(ws,{type:'ready'});snapshot();publish();return;
          }
          if(m.role==='host'&&m.room===room.id&&equal(m.key,room.hostKey)){
            if(room.observers.size>=4){ws.close(1008,'too many hosts');return;}
            role='host';room.observers.add(ws);clearTimeout(authTimer);send(ws,lobby());return;
          }
          if(m.role==='viewer'&&typeof m.code==='string'&&equal(normalizeCode(m.code),room.watchCode)){
            if(room.viewers.size>=16){ws.close(1008,'too many viewers');return;}
            role='viewer';room.viewers.add(ws);clearTimeout(authTimer);
            send(ws,{type:'watch-ready'});if(room.lastFrame)ws.send(JSON.stringify(room.lastFrame));publish();return;
          }
          if(m.role==='player'&&typeof m.code==='string'){
            const code=normalizeCode(m.code);
            if(equal(code,room.joinCode)){
              const available=i=>!room.players[i]&&!room.nativeSlots[i];
              slot=Number.isInteger(m.preferredSlot)&&m.preferredSlot>=0&&m.preferredSlot<4&&available(m.preferredSlot)?m.preferredSlot:[0,1,2,3].find(available)??-1;
              if(slot<0){send(ws,{type:'error',message:'빈 다리가 없습니다'});ws.close(1008,'room full');return;}
            }else slot=room.codes.findIndex(candidate=>equal(code,candidate));
            if(slot<0){send(ws,{type:'error',message:'방 코드 오류'});ws.close(1008,'code');return;}
            if(room.nativeSlots[slot]){send(ws,{type:'error',message:'PC가 선택한 다리'});ws.close(1008,'native occupied');return;}
            if(room.players[slot]){send(ws,{type:'error',message:'이미 연결된 다리'});ws.close(1008,'occupied');return;}
            role='player';room.players[slot]={ws,name:cleanName(m.name),bits:0,last:now,poseActive:false,poseLast:0,x:0,z:0,yaw:0};
            clearTimeout(authTimer);send(ws,{type:'joined',slot});input(slot,0);publish();return;
          }
          ws.close(1008,'key');return;
        }
        if(m.type==='ping'){send(ws,{type:'pong',sent:m.sent});return;}
        if(role==='unity'&&m.type==='native-room'&&typeof m.code==='string'&&m.code.length<=20){
          const code=m.code.replace(/[^A-Za-z0-9-]/g,'').toUpperCase();
          if(room.nativeRoomCode!==code){room.nativeRoomCode=code;publish();}
          return;
        }
        if(role==='unity'&&m.type==='native-slots'&&Number.isInteger(m.bits)&&m.bits>=0&&m.bits<=15){
          const next=Array.from({length:4},(_,i)=>!!(m.bits&(1<<i)));
          if(next.some((v,i)=>v!==room.nativeSlots[i])){
            room.nativeSlots=next;
            for(let i=0;i<4;i++)if(next[i]&&room.players[i]){
              send(room.players[i].ws,{type:'error',message:'PC가 선택한 다리'});
              room.players[i].ws.close(1008,'native occupied');
            }
            publish();
          }
          return;
        }
        if(role==='unity'&&m.type==='frame'&&typeof m.data==='string'&&m.data.length<=230000&&/^[A-Za-z0-9+/]+={0,2}$/.test(m.data)){
          room.lastFrame={type:'frame',data:m.data};
          const frame=JSON.stringify(room.lastFrame);
          for(const viewer of room.viewers)if(viewer.readyState===WebSocket.OPEN&&viewer.bufferedAmount<350000)viewer.send(frame);
          return;
        }
        if(role==='unity'&&m.type==='visual'&&['normal','giraffe','tennis','ice','squid'].includes(m.mode)){
          const changed=room.visual.mode!==m.mode,iceFrozen=m.mode==='ice'&&m.iceFrozen!==false,legSelect=m.legSelect===true;
          if(changed||room.visual.iceFrozen!==iceFrozen||!!room.visual.legSelect!==legSelect){
            room.visual={mode:m.mode,iceFrozen,legSelect};
            if(changed)for(let i=0;i<4;i++)if(room.players[i])input(i,room.players[i].bits&15);
            publish();
          }return;
        }
        if(role==='player'&&m.type==='input'&&Number.isInteger(m.bits)&&m.bits>=0&&m.bits<=255){
          const p=room.players[slot];if(p?.ws!==ws)return;p.last=now;
          // In-flight input from the previous power cannot activate the new one.
          const mask=m.mode===room.visual.mode?(actionMasks[room.visual.mode]||0):0;
          input(slot,typeof m.mode==='string'?m.bits&(15|mask):m.bits);return;
        }
        if(role==='player'&&m.type==='name'&&typeof m.name==='string'){
          const p=room.players[slot];if(p?.ws!==ws)return;const name=cleanName(m.name);if(name!==p.name){p.name=name;publish();}return;
        }
        if(role==='player'&&m.type==='switch'&&Number.isInteger(m.slot)&&m.slot>=0&&m.slot<4){
          if(m.slot===slot)return;
          if(room.players[m.slot]||room.nativeSlots[m.slot]){send(ws,{type:'error',message:'사용 중인 다리'});return;}
          const p=room.players[slot];if(!p||p.ws!==ws)return;
          room.players[slot]=null;input(slot,0);pose(slot,false);
          p.bits=0;p.poseActive=false;p.x=0;p.z=0;p.yaw=0;p.last=now;
          slot=m.slot;room.players[slot]=p;send(ws,{type:'switched',slot});input(slot,0);pose(slot,false);publish();return;
        }
        if(role==='player'&&m.type==='pose'&&typeof m.active==='boolean'){
          const p=room.players[slot];if(p?.ws!==ws)return;
          if(m.active){
            if(!Number.isFinite(m.x)||!Number.isFinite(m.z)||!Number.isFinite(m.yaw)||Math.abs(m.yaw)>3600||m.x*m.x+m.z*m.z>1.05)return;
            p.x=m.x;p.z=m.z;p.yaw=m.yaw;p.poseActive=true;p.poseLast=now;
          }else{p.poseActive=false;p.x=0;p.z=0;p.yaw=0;}
          pose(slot,p.poseActive);return;
        }
      }catch{ws.close(1008,'invalid message');}
    });
    ws.on('error',()=>{});
    ws.on('close',()=>{
      clearTimeout(authTimer);
      if(role==='unity'&&room.unity===ws){room.unity=null;room.lastFrame=null;room.nativeSlots=[false,false,false,false];room.nativeRoomCode='';room.visual={mode:'normal'};for(let i=0;i<4;i++)if(room.players[i])room.players[i].bits=0;publish();}
      if(role==='host')room.observers.delete(ws);
      if(role==='viewer'){room.viewers.delete(ws);publish();}
      if(role==='player'&&room.players[slot]?.ws===ws){room.players[slot]=null;input(slot,0);pose(slot,false);publish();}
    });
  });
  const watchdog=setInterval(()=>{
    for(let i=0;i<4;i++){const p=room.players[i];if(p?.bits&&Date.now()-p.last>500)input(i,0);if(p?.poseActive&&Date.now()-p.poseLast>500){p.poseActive=false;pose(i,false);}}
    // Host receives a complete state periodically, including zero releases.
    snapshot();
  },100);
  const heartbeat=setInterval(()=>{for(const ws of wss.clients){if(!ws.alive){ws.terminate();continue;}ws.alive=false;ws.ping();}},10000);
  await new Promise(r=>server.listen(port,'127.0.0.1',r));
  const actualPort=server.address().port;
  if(writeConfig){
    const config={url:`ws://127.0.0.1:${actualPort}/ws`,room:room.id,key:room.hostKey};
    await mkdir(resolve(root,'.runtime'),{recursive:true});
    await writeFile(resolve(root,'.runtime/PhoneController.json'),JSON.stringify(config,null,2));
    // Editor reads its project config; standalone reads Unity's per-user persistentDataPath.
    // Never place the host key beside a portable player that may be sent to guests.
    let remembered='';try{remembered=(await readFile(resolve(root,'.runtime/unity-project.txt'),'utf8')).trim();}catch{}
    const targets=new Set([process.env.UNITY_PROJECT||remembered]);
    for(const unityProject of targets)if(unityProject&&existsSync(resolve(unityProject,'Assets'))){
      await writeFile(resolve(unityProject,'PhoneController.json'),JSON.stringify(config,null,2));
    }
    if(process.env.USERPROFILE){
      const persistent=resolve(process.env.USERPROFILE,'AppData/LocalLow/Team Goonanhooo/we are table');
      await mkdir(persistent,{recursive:true});
      await writeFile(resolve(persistent,'PhoneController.json'),JSON.stringify(config,null,2));
    }
  }
  return {server,room,port:actualPort,close:async()=>{if(closed)return;closed=true;clearInterval(watchdog);clearInterval(heartbeat);for(const ws of wss.clients)ws.terminate();await new Promise(r=>server.close(r));}};
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const relay=await createRelay({port:Number(process.env.PORT||4180),writeConfig:true});
  console.log(`Phone controller host: http://127.0.0.1:${relay.port}/host`);
  for(const sig of ['SIGINT','SIGTERM'])process.on(sig,async()=>{await relay.close();process.exit(0);});
}
