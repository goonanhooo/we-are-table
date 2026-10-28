import {DurableObject} from 'cloudflare:workers';
import QRCode from 'qrcode/lib/server.js';

const alphabet='ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const normalize=value=>String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'');
const code=()=>Array.from(crypto.getRandomValues(new Uint8Array(8)),v=>alphabet[v%32]).join('');
const secret=()=>Array.from(crypto.getRandomValues(new Uint8Array(24)),v=>v.toString(16).padStart(2,'0')).join('');
const masks={normal:0,giraffe:48,tennis:192,ice:48,squid:16};
const headers={'Content-Type':'application/json','Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer'};
const json=(value,status=200)=>new Response(JSON.stringify(value),{status,headers});
const equal=(a,b)=>{if(typeof a!=='string'||typeof b!=='string'||a.length!==b.length)return false;let n=0;for(let i=0;i<a.length;i++)n|=a.charCodeAt(i)^b.charCodeAt(i);return n===0;};
const send=(ws,value)=>{try{if(ws?.readyState===1)ws.send(JSON.stringify(value));}catch{}};
const close=(ws,reason)=>{try{ws.close(1008,reason);}catch{}};
const rejectSocket=reason=>{const [client,server]=Object.values(new WebSocketPair());server.accept();server.close(1008,reason);return new Response(null,{status:101,webSocket:client});};

export default {
  async fetch(request,env){
    const url=new URL(request.url);
    if(url.pathname==='/health')return json({ok:true,service:'we-are-table-controller',version:2});
    if(url.pathname==='/api/rooms'&&request.method==='POST'){
      const origin=request.headers.get('Origin');
      if(origin&&origin!==url.origin)return json({error:'origin'},403);
      if(Number(request.headers.get('Content-Length')||0)>1024)return json({error:'body'},413);
      const limiter=await env.ROOM_CREATION_LIMITER.limit({key:request.headers.get('CF-Connecting-IP')||'unknown'});
      if(!limiter.success)return json({error:'Please wait before creating another room.'},429);
      const directory=env.DIRECTORY.get(env.DIRECTORY.idFromName('directory'));
      return directory.fetch('https://internal/new',{method:'POST',body:JSON.stringify({origin:url.origin})});
    }
    if(url.pathname==='/ws'){
      if(request.headers.get('Upgrade')?.toLowerCase()!=='websocket')return json({error:'websocket required'},426);
      const origin=request.headers.get('Origin');
      if(origin&&origin!==url.origin)return json({error:'origin'},403);
      const value=normalize(url.searchParams.get('code'));
      if(!/^[A-Z0-9]{8}$/.test(value))return json({error:'room code required'},400);
      const directory=env.DIRECTORY.get(env.DIRECTORY.idFromName('directory'));
      const lookup=await directory.fetch('https://internal/lookup?code='+value);
      if(!lookup.ok)return rejectSocket('room code');
      const {room}=await lookup.json();
      return env.ROOMS.get(env.ROOMS.idFromName(room)).fetch(request);
    }
    if(url.pathname.startsWith('/api/'))return json({error:'not available publicly'},403);
    if(url.pathname==='/host')return new Response('폰 초대 QR은 게임의 함께하기 로비에서 확인하세요. / Open the phone invite QR in the game lobby.',{headers:{'Content-Type':'text/plain; charset=utf-8','Cache-Control':'no-store'}});
    const response=await env.ASSETS.fetch(request);
    const result=new Response(response.body,response);
    result.headers.set('X-Content-Type-Options','nosniff');
    result.headers.set('Referrer-Policy','no-referrer');
    result.headers.set('Content-Security-Policy',"default-src 'self'; connect-src 'self' wss:; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'self'");   // 'self' = UI 미리보기(preview.html)가 같은 주소의 컨트롤러를 iframe으로 띄움
    if(!url.pathname.startsWith('/assets/'))result.headers.set('Cache-Control','no-store');
    return result;
  }
};

export class RoomDirectory extends DurableObject {
  constructor(ctx,env){super(ctx,env);this.ctx=ctx;this.env=env;}
  async fetch(request){
    const url=new URL(request.url),now=Date.now();
    if(url.pathname==='/new'){
      const {origin}=await request.json();
      const existing=await this.ctx.storage.list({prefix:'code:'});
      let joinCode=code(),watchCode=code();
      while(existing.has('code:'+joinCode))joinCode=code();
      while(watchCode===joinCode||existing.has('code:'+watchCode))watchCode=code();
      const room=crypto.randomUUID(),key=secret(),expires=now+7*24*60*60*1000;
      const config={room,key,joinCode,watchCode,origin,expires};
      const init=await this.env.ROOMS.get(this.env.ROOMS.idFromName(room)).fetch('https://internal/init',{method:'POST',body:JSON.stringify(config)});
      if(!init.ok)return json({error:'room initialization'},500);
      await this.ctx.storage.put({['code:'+joinCode]:{room,expires},['code:'+watchCode]:{room,expires}});
      if(!await this.ctx.storage.getAlarm())await this.ctx.storage.setAlarm(now+60*60*1000);
      return json({url:origin.replace('https:','wss:')+'/ws?code='+joinCode,room,key,joinCode,watchCode,origin,expires},201);
    }
    if(url.pathname==='/lookup'){
      const info=await this.ctx.storage.get('code:'+url.searchParams.get('code'));
      return info&&info.expires>now?json(info):json({error:'not found'},404);
    }
    return json({error:'not found'},404);
  }
  async alarm(){
    const all=await this.ctx.storage.list({prefix:'code:'});
    const expired=[...all].filter(([,v])=>v.expires<=Date.now()).map(([k])=>k);
    if(expired.length)await this.ctx.storage.delete(expired);
    if(all.size>expired.length)await this.ctx.storage.setAlarm(Date.now()+60*60*1000);
  }
}

export class PhoneRoom extends DurableObject {
  constructor(ctx,env){
    super(ctx,env);this.ctx=ctx;this.env=env;this.clients=new Map();this.room=null;this.lastFrame=null;
    ctx.blockConcurrencyWhile(async()=>{
      this.room=await ctx.storage.get('room');
      for(const ws of ctx.getWebSockets()){const info=ws.deserializeAttachment();if(info)this.clients.set(ws,info);}
    });
  }
  list(role){return [...this.clients].filter(([ws,p])=>ws.readyState===1&&p.role===role);}
  unity(){return this.list('unity')[0]?.[0];}
  players(){const slots=Array(4).fill(null);for(const [ws,p] of this.list('player'))slots[p.slot]={ws,...p};return slots;}
  native(){return this.unity()?this.room.nativeSlots:[false,false,false,false];}
  persist(ws,p){this.clients.set(ws,p);ws.serializeAttachment(p);}
  async save(){await this.ctx.storage.put('room',this.room);}
  frame(ws,p,value){if(p.framePending)return;p.framePending=true;this.persist(ws,p);send(ws,value);}
  status(){const players=this.players(),native=this.native();return {type:'status',unity:!!this.unity(),occupiedSlots:players.map((p,i)=>!!p||native[i]),nativeSlots:native,players:players.map(p=>p?{name:p.name,active:!!p.bits}:null),visual:this.room.visual};}
  lobby(){const players=this.players();return {type:'lobby',room:this.room.room,codes:[],joinCode:this.room.joinCode,joinQrPng:this.room.joinQrPng,watchCode:this.room.watchCode,nativeRoomCode:this.unity()?this.room.nativeRoomCode:'',origin:this.room.origin,occupiedSlots:players.map(Boolean),nativeSlots:this.native(),unity:!!this.unity(),viewers:this.list('viewer').length};}
  publish(){const status=this.status();for(const [ws] of this.list('player'))send(ws,status);send(this.unity(),this.lobby());for(const [ws] of this.list('host'))send(ws,this.lobby());for(const [ws] of this.list('viewer'))send(ws,{type:'watch-status',unity:!!this.unity(),viewers:this.list('viewer').length});}
  input(slot,bits){const p=this.players()[slot];if(p){p.bits=bits;const {ws,...data}=p;this.persist(ws,data);}send(this.unity(),{type:'input',slot,bits,occupied:!!p});}
  pose(slot,active){const p=this.players()[slot];send(this.unity(),{type:'pose',slot,active:!!p&&active,x:active?p?.x||0:0,z:active?p?.z||0:0,yaw:active?p?.yaw||0:0});}
  snapshot(){for(let i=0;i<4;i++){const p=this.players()[i];this.input(i,p?.bits&&Date.now()-p.last<=500?p.bits:0);this.pose(i,!!p?.poseActive&&Date.now()-p.poseLast<=500);}}
  async schedule(ms=650){const when=Date.now()+ms,current=await this.ctx.storage.getAlarm();if(!current||current>when)await this.ctx.storage.setAlarm(when);}
  async fetch(request){
    if(new URL(request.url).pathname==='/init'){
      if(this.room)return json({error:'already initialized'},409);
      const config=await request.json();
      const png=await QRCode.toBuffer(config.origin+'/#code='+config.joinCode,{type:'png',margin:2,width:320});
      this.room={...config,joinQrPng:png.toString('base64'),nativeSlots:[false,false,false,false],nativeRoomCode:'',visual:{mode:'normal'}};
      await this.save();await this.ctx.storage.setAlarm(config.expires);return json({ok:true});
    }
    if(!this.room||this.room.expires<=Date.now())return rejectSocket('room expired');
    if(this.ctx.getWebSockets().length>=32)return rejectSocket('capacity');
    const pair=new WebSocketPair(),[client,server]=Object.values(pair);
    this.ctx.acceptWebSocket(server);
    this.persist(server,{role:'',created:Date.now(),count:0,window:Date.now()});
    await this.schedule(5000);
    return new Response(null,{status:101,webSocket:client});
  }
  async webSocketMessage(ws,raw){
    try{
      if(typeof raw!=='string'||raw.length>262144){close(ws,'invalid message');return;}
      const p=this.clients.get(ws);if(!p)return;
      const now=Date.now();if(now-p.window>1000){p.count=0;p.window=now;}if(++p.count>100){close(ws,'rate');return;}
      const m=JSON.parse(raw);
      if(!p.role){
        if(m.type!=='hello'){close(ws,'hello required');return;}
        if((m.role==='unity'||m.role==='host')&&m.room===this.room.room&&equal(m.key,this.room.key)){
          if(m.role==='unity'&&this.unity()){close(ws,'host already connected');return;}
          if(m.role==='host'&&this.list('host').length>=4){close(ws,'capacity');return;}
          p.role=m.role;this.persist(ws,p);
          if(p.role==='unity'){send(ws,{type:'ready'});this.snapshot();this.publish();}else send(ws,this.lobby());return;
        }
        if(m.role==='viewer'&&equal(normalize(m.code),this.room.watchCode)){
          if(this.list('viewer').length>=16){close(ws,'capacity');return;}
          p.role='viewer';p.framePending=false;this.persist(ws,p);send(ws,{type:'watch-ready'});if(this.lastFrame)this.frame(ws,p,this.lastFrame);this.publish();return;
        }
        if(m.role==='player'&&equal(normalize(m.code),this.room.joinCode)){
          const players=this.players(),native=this.native(),available=i=>!players[i]&&!native[i];
          const slot=Number.isInteger(m.preferredSlot)&&m.preferredSlot>=0&&m.preferredSlot<4&&available(m.preferredSlot)?m.preferredSlot:[0,1,2,3].find(available)??-1;
          if(slot<0){send(ws,{type:'error',message:'빈 다리가 없습니다'});close(ws,'room full');return;}
          Object.assign(p,{role:'player',slot,name:String(m.name||'플레이어').replace(/[\x00-\x1f]/g,'').slice(0,16),bits:0,last:now,poseActive:false,poseLast:0,x:0,z:0,yaw:0});
          this.persist(ws,p);send(ws,{type:'joined',slot});this.input(slot,0);this.publish();return;
        }
        close(ws,'key');return;
      }
      this.persist(ws,p);
      if(m.type==='ping'){send(ws,{type:'pong',sent:m.sent});return;}
      if(p.role==='unity'){
        if(m.type==='native-room'&&typeof m.code==='string'&&m.code.length<=20){const c=m.code.replace(/[^A-Za-z0-9-]/g,'').toUpperCase();if(c!==this.room.nativeRoomCode){this.room.nativeRoomCode=c;await this.save();this.publish();}return;}
        if(m.type==='native-slots'&&Number.isInteger(m.bits)&&m.bits>=0&&m.bits<=15){
          const next=Array.from({length:4},(_,i)=>!!(m.bits&(1<<i)));
          if(next.some((v,i)=>v!==this.room.nativeSlots[i])){
            this.room.nativeSlots=next;await this.save();
            for(const [other,player] of this.list('player'))if(next[player.slot]){send(other,{type:'error',message:'PC가 선택한 다리'});close(other,'native occupied');}
            this.publish();
          }return;
        }
        if(m.type==='visual'&&Object.hasOwn(masks,m.mode)){
          const changed=this.room.visual.mode!==m.mode,iceFrozen=m.mode==='ice'&&m.iceFrozen!==false;
          if(changed||this.room.visual.iceFrozen!==iceFrozen){this.room.visual={mode:m.mode,iceFrozen};if(changed)for(const [,player] of this.list('player'))this.input(player.slot,player.bits&15);await this.save();this.publish();}return;
        }
        if(m.type==='frame'&&typeof m.data==='string'&&m.data.length<=230000&&/^[A-Za-z0-9+/]+={0,2}$/.test(m.data)){this.lastFrame={type:'frame',data:m.data};for(const [viewer,info] of this.list('viewer'))this.frame(viewer,info,this.lastFrame);return;}
        return;
      }
      if(p.role==='viewer'){if(m.type==='frame-ack'){p.framePending=false;this.persist(ws,p);}return;}
      if(p.role!=='player')return;
      if(m.type==='input'&&Number.isInteger(m.bits)&&m.bits>=0&&m.bits<=255){
        p.last=now;this.persist(ws,p);const mask=m.mode===this.room.visual.mode?masks[m.mode]:0;
        this.input(p.slot,m.bits&(15|mask));if(m.bits)await this.schedule();return;
      }
      if(m.type==='pose'&&typeof m.active==='boolean'){
        if(m.active&&(![m.x,m.z,m.yaw].every(Number.isFinite)||Math.abs(m.yaw)>3600||m.x*m.x+m.z*m.z>1.05))return;
        Object.assign(p,{poseActive:m.active,poseLast:now,x:m.active?m.x:0,z:m.active?m.z:0,yaw:m.active?m.yaw:0});this.persist(ws,p);this.pose(p.slot,p.poseActive);if(m.active)await this.schedule();return;
      }
      if(m.type==='switch'&&Number.isInteger(m.slot)&&m.slot>=0&&m.slot<4&&m.slot!==p.slot){
        if(this.players()[m.slot]||this.native()[m.slot]){send(ws,{type:'error',message:'사용 중인 다리'});return;}
        const old=p.slot;Object.assign(p,{slot:m.slot,bits:0,poseActive:false,x:0,z:0,yaw:0,last:now});this.persist(ws,p);
        this.input(old,0);this.pose(old,false);send(ws,{type:'switched',slot:m.slot});this.input(m.slot,0);this.pose(m.slot,false);this.publish();
      }
    }catch{close(ws,'invalid message');}
  }
  async webSocketClose(ws,code,reason,wasClean){
    const p=this.clients.get(ws);this.clients.delete(ws);
    try{ws.close(code===1005||code===1006?1000:code,reason);}catch{}
    if(p?.role==='unity'){
      this.room.nativeSlots=[false,false,false,false];this.room.nativeRoomCode='';this.room.visual={mode:'normal'};this.lastFrame=null;
      for(const [phone,player] of this.list('player')){player.bits=0;player.poseActive=false;this.persist(phone,player);}await this.save();
    }
    if(p?.role==='player'){this.input(p.slot,0);this.pose(p.slot,false);}
    this.publish();
  }
  async webSocketError(ws){await this.webSocketClose(ws,1011,'connection error',false);}
  async alarm(){
    if(!this.room)return;
    if(this.room.expires<=Date.now()){
      for(const [ws] of this.clients)close(ws,'room expired');await this.ctx.storage.deleteAll();this.room=null;return;
    }
    const now=Date.now();let active=false;
    for(const [ws,p] of this.clients){
      if(!p.role){if(now-p.created>=5000)close(ws,'authentication required');else active=true;continue;}
      if(p.role==='player'){
        if(p.bits&&now-p.last>500)this.input(p.slot,0);
        if(p.poseActive&&now-p.poseLast>500){p.poseActive=false;this.persist(ws,p);this.pose(p.slot,false);}
        if(p.bits||p.poseActive)active=true;
      }
    }
    await this.ctx.storage.setAlarm(active?now+650:this.room.expires);
  }
}
