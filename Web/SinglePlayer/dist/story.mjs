import {MAPS,CHECKPOINTS,POWERS,buildMap} from './maps.mjs';
import {rotate,clamp} from './physics.mjs';

const SAVE_KEY='we-are-table-single-v2';
const defaults=()=>({version:2,map:'Hallway',quest:0,powers:0,checkpoint:0,reached:[0],flags:{},position:null,completed:[],bestRace:null});
const distance=(a,b)=>Math.hypot(a.x-b[0],a.y-b[1],a.z-b[2]);
const horizontal=(a,b)=>Math.hypot(a.x-b[0],a.z-b[2]);
export class StoryFlow {
  constructor(sim,{storage=null,onMap=()=>{},onMessage=()=>{}}={}){
    this.sim=sim;this.storage=storage;this.onMap=onMap;this.onMessage=onMessage;this.save=defaults();this.map=null;this.spec=null;this.quest=0;this.flags={};this.events=[];this.elapsed=0;this.timer=0;this.cutscene=null;this.ending=false;this.practice=false;
    try{const stored=JSON.parse(storage?.getItem(SAVE_KEY)||'null');if(stored?.version===2&&MAPS.some(m=>m.id===stored.map)&&Number.isInteger(stored.quest)&&stored.quest>=0&&stored.quest<=8)this.save={...defaults(),...stored,powers:clamp(Number(stored.powers)||0,0,15),flags:stored.flags&&typeof stored.flags==='object'?stored.flags:{},reached:Array.isArray(stored.reached)?stored.reached.filter(i=>Number.isInteger(i)&&i>=0&&i<16):[0]};}catch{}
  }
  persist(){try{this.storage?.setItem(SAVE_KEY,JSON.stringify(this.save));}catch{this.message('현재 브라우저에서 저장을 사용할 수 없어 이번 실행에서만 진행됩니다.');}}
  message(text){this.events.push(text);this.onMessage(text);}
  get powers(){return this.practice?15:this.save.powers;}
  grant(mode){const p=POWERS.find(p=>p.id===mode);if(!p)return;this.save.powers|=p.bit;this.sim.setMode(mode);this.message(`${p.name}의 힘을 얻었어요. ${p.key}번 키로 켜고 끌 수 있습니다.`);}
  selectPower(mode){if(mode==='normal'||(this.powers&POWERS.find(p=>p.id===mode)?.bit)){this.sim.setMode(this.sim.mode===mode?'normal':mode);return true;}this.message('이 능력은 여정에서 먼저 만나야 해요.');return false;}
  enter(id,{resume=false,practice=false}={}){
    const map=MAPS.find(m=>m.id===id)||MAPS[0];this.map=map.id;this.practice=practice||id==='FlatGround';this.timer=0;this.elapsed=0;this.cutscene=null;this.ending=false;this.buttons=[false,false];this.cartRide=null;
    this.quest=resume?this.save.quest:0;this.flags=resume?{...this.save.flags}:{};this.gateAlign=0;this.kitchenAlign=0;this.socketMask=0;
    if(!resume&&!practice&&map.entry!==null){const minimum={Hallway:0,Jungle:0,IceCavern:1,Space:7,Moon:15,Moon_CozyKitchen:15}[id];this.save.powers|=minimum;}
    this.spec=buildMap(map.id,this.flags);this.sim.setEnvironment(this.spec);this.sim.setMode('normal');
    if(resume&&this.save.position){const p=this.save.position;this.sim.reset(p.x,p.z,p.heading||0,p.floor||0);}
    if(this.map==='Space'){this.sim.setMode('squid');if(!resume)this.cutscene={type:'space-intro',time:0,duration:2};}
    if(this.map==='Moon'&&!resume){for(const b of [this.sim.board,...this.sim.legs.flatMap(l=>[l.body,l.yawBody])])b.setLinvel({x:0,y:-2,z:0},true);}
    if(this.map==='Soccer'){this.score=[0,0];this.matchTime=180;this.goalDelay=0;}
    if(this.map==='Race'){this.raceGate=0;this.raceTime=-4;this.result=null;}
    if(!practice&&map.entry!==null&&!resume&&this.map!=='Space')this.checkpoint(map.entry,false);
    this.onMap(this.spec);return this.spec;
  }
  start(){this.save=defaults();this.persist();return this.enter('Hallway');}
  resume(){return this.enter(this.save.map,{resume:true});}
  checkpoint(id,notify=true){
    if(this.practice)return;
    if(!this.save.reached.includes(id))this.save.reached.push(id);
    const p=this.sim.board.translation(),floor=this.sim.floorAt(p.x,p.z);
    const safe=floor!==null&&Math.abs(floor)<30?floor:this.spec.spawn.floor||0;
    this.save={...this.save,map:this.map,quest:this.quest,flags:{...this.flags},checkpoint:id,position:{x:p.x,z:p.z,floor:safe,heading:0}};
    // A cart checkpoint must restore to a stable station rather than over a chasm.
    if(id===7)this.save.position={x:0,z:8,floor:1.1,heading:0};
    if(id===8)this.save.position={x:0,z:18.8,floor:3,heading:0};
    if(id===9)this.save.position={x:0,z:24,floor:3,heading:0};
    if(id===10)this.save.position={x:0,z:38,floor:0,heading:0};
    if(id===11||id===12)this.save.position={x:p.x,z:p.z,floor:p.y-.9,heading:0};
    if(id===13)this.save.position={x:0,z:-6,floor:0,heading:0};
    if(id===14)this.save.position={x:0,z:-3,floor:0,heading:0};
    this.persist();if(notify)this.message(`체크포인트 · ${CHECKPOINTS[id][1]}`);
  }
  complete(next){
    if(!this.save.completed.includes(this.map))this.save.completed.push(this.map);
    this.sim.releaseAll();this.enter(next);this.persist();
  }
  restart(){
    if(['Soccer','Race','FlatGround'].includes(this.map)){this.enter(this.map,{practice:this.practice});return;}
    this.enter(this.map,{resume:true});this.message('마지막 체크포인트에서 다시 시작했어요.');
  }
  recover(){
    const p=this.sim.board.translation(),floor=this.sim.floorAt(p.x,p.z);
    if(floor===null||p.y<-1||this.cartRide){this.restart();return;}
    const traveled=this.sim.distance;this.sim.recover();this.sim.distance=traveled;this.onMap(this.spec);this.message('현재 자리에서 바로 세웠어요.');
  }
  locked(){return !!this.cutscene||this.ending||(this.map==='Race'&&this.raceTime<0);}
  target(){
    const f=this.spec?.features||{};
    if(this.map==='Hallway')return f.trap?.position;
    if(this.map==='Jungle')return [f.cage,f.crossed,f.gate,f.exit][Math.min(this.quest,3)]?.position;
    if(this.map==='IceCavern'){
      if(this.quest===0)return f.caterpillar.position;
      if(this.quest===1){const p=this.sim.objects.get('cart-a').body.translation();return [p.x,p.y+.2,p.z];}
      if(this.quest===2){const p=this.sim.objects.get('cart-b').body.translation();return [p.x,p.y+.2,p.z];}
      if(this.quest===3)return f.snowman.position;
      if(this.quest===4)return this.sim.board.translation().z<26.5?f.slide.position:f.landing.position;
      return f.rocket.position;
    }
    if(this.map==='Space')return f.moon.position;
    if(this.map==='Moon')return f.home.position;
    if(this.map==='Moon_CozyKitchen')return f.sockets.position;
    if(this.map==='Soccer'){const p=this.sim.objects.get('football').body.translation();return [p.x,p.y,p.z];}
    if(this.map==='Race')return f['race-'+Math.min(this.raceGate,3)]?.position;
    return f.practice.position;
  }
  objective(){
    const remaining=Math.max(0,Math.ceil(this.matchTime||0));
    if(this.cutscene){return {title:{fall:'구름 너머로 떨어지는 중',rocket:'의자를 따라 우주로', 'space-intro':'우주오징어를 만났어요',capture:'달의 중력에 이끌리는 중',ending:'모두 집에 도착했어요'}[this.cutscene.type],hint:this.cutscene.type==='ending'?'여섯 장면의 부엌 엔딩 · C로 마무리':'잠시 기다리거나 C로 연출을 건너뛸 수 있어요.'};}
    const objectives={
      Hallway:[['의자를 따라 복도 끝으로','네 다리를 직접 움직이거나 Space를 눌러 목표 방향으로 걸어 보세요.']],
      Jungle:[['기린 앞 버튼을 눌러 주세요','기린 앞으로 다가가 C를 누르거나 발로 버튼을 밟으세요.'],['기린 다리로 용암 협곡을 건너세요','2 기린 · = 네 다리 늘리기 · − 줄이기. 긴 다리로 바위 사이를 건너세요.'],['두 스위치를 함께 눌러 주세요','2를 꺼 다리를 줄인 뒤 두 발판에 발을 올려 주세요. 가까이서 C로 자세를 정돈할 수 있어요.'],['열린 구멍으로 내려가세요','앞쪽 석문 아래의 구멍으로 들어가면 동굴로 이어집니다.']],
      IceCavern:[['테니스공 애벌레를 만나세요','가까이에서 C · 3으로 테니스공을 켜세요.'],['첫 광차에 올라 발을 붙이세요','광차 위에서 C 또는 E / Y / O / ]로 붙이기. Q / R / U / P로 해제.'],['두 번째 광차로 갈아타세요','첫 광차 끝에서 C로 내려 역으로 걸은 뒤, 다음 광차 위에서 C로 붙이세요.'],['눈사람의 머리를 올려 주세요','3 테니스공으로 머리를 잡고 다리를 스윙해 눈사람 위로 들어 올리세요. 2 기린으로 먼저 길게 늘릴 수 있어요.'],['얼음으로 미끄럼틀을 내려가세요','4 얼음 · Space로 미끄럼틀 입구까지 간 뒤 경사를 타고 내려가세요.'],['로켓에 올라타세요','발사대 가까이에서 C를 누르면 우주로 출발합니다.']],
      Space:[['달을 향해 날아가세요','5 오징어 · E / Y / O / ] 분사. Space는 네 다리를 목표 방향으로 조준해 분사합니다.']],
      Moon:[['의자가 기다리는 집으로','달의 중력은 지구의 약 1/6. 집 입구 가까이에서 C를 누르세요.']],
      Moon_CozyKitchen:[['네 발을 네 자리에 놓으세요','바닥의 네 원에 발이 모두 닿으면 부엌 엔딩이 시작돼요. C는 가까이에서 자세를 정돈합니다.']],
      Soccer:[[`골 ${this.score?.[0]||0} : ${this.score?.[1]||0} · ${Math.floor(remaining/60)}:${String(remaining%60).padStart(2,'0')}`,'공을 앞쪽 골대로 밀어 넣으세요. 3골 또는 시간 종료 시 결과가 표시됩니다. C로 공을 다시 놓습니다.']],
      Race:[[`반환점 ${Math.min((this.raceGate||0)+1,4)} / 4 · ${Math.max(0,this.raceTime||0).toFixed(1)}초`,'표시된 게이트를 순서대로 통과하세요. 결승은 반환점을 모두 지난 뒤 인정됩니다.']],
      FlatGround:[['모든 능력을 자유롭게 실험하세요','2 기린 · 3 테니스공 · 4 얼음 · 5 오징어. 맵 선택에서 여정을 시작할 수 있어요.']],
    };
    if(this.ending)return {title:this.map==='Race'?`완주! ${this.result?.toFixed(2)}초`:this.map==='Soccer'?`연습 종료 · ${this.score[0]} : ${this.score[1]}`:'함께, 집으로.',hint:'맵 선택에서 다른 맵을 플레이하거나 다시 시작할 수 있어요.'};
    if(this.map==='Race'&&this.raceTime<0)return {title:`출발까지 ${Math.ceil(-this.raceTime-.0001)}`,hint:'잠시 뒤 출발합니다.'};
    const item=objectives[this.map]?.[Math.min(this.quest,(objectives[this.map]?.length||1)-1)]||['여행 중',''];return {title:item[0],hint:item[1]};
  }
  interact(){
    if(this.cutscene){this.cutscene.time=this.cutscene.duration;return;}
    const p=this.sim.board.translation(),f=this.spec.features;
    if(this.map==='Jungle'&&this.quest===0&&horizontal(p,f.cage.position)<2.2){this.freeGiraffe();return;}
    if(this.map==='Jungle'&&this.quest===2&&horizontal(p,f.gate.position)<2){this.gateAlign=4;this.sim.setMode('normal');this.message('두 스위치 위로 발을 정돈하고 있어요.');return;}
    if(this.map==='IceCavern'){
      if(this.quest===0&&horizontal(p,f.caterpillar.position)<2.8){this.flags.tennis=true;this.quest=1;this.grant('tennis');this.checkpoint(6);return;}
      if(this.quest===1||this.quest===2){
        if(this.cartRide){if(this.cartProgress>=1){this.sim.releaseAll();this.cartRide=null;this.message('발을 뗐어요. 다음 광차로 걸어서 이동하세요.');}else this.message('운행 중이에요. 도착한 뒤 발을 떼세요.');return;}
        const id=this.quest===1?'cart-a':'cart-b',cart=this.sim.objects.get(id),cp=cart.body.translation();
        if(horizontal(p,[cp.x,cp.y,cp.z])<1.8&&Math.abs(p.y-(cp.y+.95))<2){
          if(!(this.powers&2)){this.message('테니스공의 힘이 필요해요.');return;}
          this.sim.setMode('tennis');let attached=0;for(let i=0;i<4;i++)if(this.sim.grab(i,id))attached++;
          if(attached){this.cartRide=id;this.cartProgress=0;this.cartStart={...cp};this.message('발이 광차에 붙었어요. 출발합니다.');}else this.message('광차 윗면에 발이 닿도록 올라가세요.');return;
        }
      }
      if(this.quest===5&&horizontal(p,f.rocket.position)<3.2){this.sim.releaseAll();this.sim.setMode('normal');this.cutscene={type:'rocket',time:0,duration:8,start:{...p}};this.message('3, 2, 1… 함께 우주로!');return;}
    }
    if(this.map==='Moon'&&horizontal(p,f.home.position)<2.8&&p.y<3){this.checkpoint(14);this.complete('Moon_CozyKitchen');return;}
    if(this.map==='Moon_CozyKitchen'&&horizontal(p,f.sockets.position)<1.5){
      // A soft force and zero-angle motors help a single player align; socket contact still gates the ending.
      this.kitchenAlign=2;this.sim.setMode('normal');this.message('네 발을 제자리에 정돈하고 있어요.');return;
    }
    if(this.map==='Soccer'){this.resetBall();return;}
    this.message(this.objective().hint);
  }
  freeGiraffe(){this.flags.giraffe=true;this.quest=1;this.sim.removeBox('cage-button');this.sim.addBox({id:'cage-button',position:[0,.002,1],size:[1.1,.004,1.1],color:'#92b66d'});this.grant('giraffe');this.checkpoint(2);}
  resetBall(){const b=this.sim.objects.get('football').body;b.setTranslation({x:0,y:.5,z:0},true);b.setLinvel({x:0,y:0,z:0},true);b.setAngvel({x:0,y:0,z:0},true);}
  update(dt){
    this.elapsed+=dt;const p=this.sim.board.translation(),f=this.spec.features;
    if(this.cutscene){
      const c=this.cutscene;c.time+=dt;
      if(c.type==='rocket'&&c.time>3){const v={x:0,y:(c.time-3)*5,z:0};this.moveTable({...c.start,y:c.start.y+v.y});}
      if(c.type==='capture'){
        const target=f.moon.position,d={x:target[0]-p.x,y:target[1]-p.y,z:target[2]-p.z},r=Math.hypot(d.x,d.y,d.z),a=clamp(7000/(r*r),0,30)*7.6;
        this.sim.externalForce={x:d.x/r*a,y:d.y/r*a,z:d.z/r*a};
      }
      if(c.time>=c.duration){
        this.cutscene=null;this.sim.externalForce=null;
        if(c.type==='fall')this.complete('Jungle');
        if(c.type==='rocket')this.complete('Space');
        if(c.type==='space-intro'){this.grant('squid');this.checkpoint(11);}
        if(c.type==='capture')this.complete('Moon');
        if(c.type==='ending'){this.ending=true;this.flags.finished=true;this.checkpoint(15);if(!this.save.completed.includes(this.map))this.save.completed.push(this.map);this.persist();this.message('모두 집에 도착했어요. 함께해 줘서 고마워요.');}
      }
      return;
    }
    if(this.ending)return;
    if(!Number.isFinite(p.y)||p.y < -8||(this.map!=='Space'&&Math.abs(p.x)>80)||p.z>190){this.restart();return;}
    if(this.map==='Hallway'){
      if(!this.flags.trap&&p.z>=6.2){this.flags.trap=true;this.sim.removeBox('trap');this.message('바닥이 열리고 있어요.');}
      if(this.flags.trap&&p.y<-1.5)this.cutscene={type:'fall',time:0,duration:1.2};
    }else if(this.map==='Jungle'){
      if(this.gateAlign>0){this.gateAlign-=dt;this.sim.alignTarget={x:0,z:14};}else this.sim.alignTarget=null;
      if(p.z>6.5&&p.z<8.5&&this.sim.legs.some(l=>this.sim.foot(l).y<-.70)){this.restart();this.message('용암에 닿았어요. 마지막 안전한 곳에서 다시 시작합니다.');return;}
      if(this.quest===0&&this.sim.legs.some(l=>{const t=this.sim.foot(l);return horizontal(t,f.cage.position)<.8&&t.y<.4;}))this.freeGiraffe();
      if(this.quest===1&&p.z>10.2&&p.y>0&&this.sim.legs.some(l=>l.footSupported)&&this.sim.legs.some(l=>l.length>1.3)){this.quest=2;this.checkpoint(3);}
      if(this.quest===2){
        this.buttons=[-.58,.58].map(x=>this.sim.legs.some(l=>{const t=this.sim.foot(l);return Math.hypot(t.x-x,t.z-14.58)<.47&&Math.abs(t.y-.12)<.26;}));
        this.timer=this.buttons.every(Boolean)?this.timer+dt:0;
        if(this.timer>.35){this.quest=3;this.flags.gate=true;this.sim.removeBox('jungle-lid');this.sim.releaseAll();for(const [i,x] of [[0,-.58],[1,.58]]){this.sim.removeBox('gate-pad-'+i);this.sim.addBox({id:'gate-pad-'+i,position:[x,.002,14.58],size:[.7,.004,.7],color:'#91b77b'});}this.checkpoint(4);this.message('두 스위치가 눌렸어요. 동굴로 가는 문이 열립니다.');}
      }
      if(this.quest===3&&horizontal(p,f.exit.position)<1.5&&p.y<-.8)this.complete('IceCavern');
    }else if(this.map==='IceCavern'){
      if(this.cartRide){
        const id=this.cartRide,cart=this.sim.objects.get(id),end=id==='cart-a'?{x:0,y:.9,z:7.5}:{x:0,y:2.8,z:17.5};
        this.cartProgress=clamp(this.cartProgress+dt/5,0,1);const u=this.cartProgress;
        cart.body.setNextKinematicTranslation({x:0,y:this.cartStart.y+(end.y-this.cartStart.y)*u,z:this.cartStart.z+(end.z-this.cartStart.z)*u});
        if(u===1&&!this.flags[id]){this.flags[id]=true;
          if(id==='cart-a'){this.quest=2;this.checkpoint(7);}else{this.quest=3;this.sim.releaseAll();this.cartRide=null;this.checkpoint(8);}this.message('광차가 도착했어요. C로 발을 뗄 수 있습니다.');}
      }
      if(this.quest===3){const head=this.sim.objects.get('snow-head').body,h=head.translation();if(distance(h,f['head-target'].position)<1.15&&h.y>4.05){this.sim.releaseAll();this.sim.freezeObject('snow-head',{x:0,y:4.95,z:21.5});this.flags.snowman=true;this.quest=4;this.grant('ice');this.checkpoint(9);}}
      if(this.quest===4&&this.sim.mode==='ice'&&p.z>36.6&&p.y<2){this.quest=5;this.checkpoint(10);this.message('얼음 길을 내려왔어요. 이제 로켓으로 가세요.');}
    }else if(this.map==='Space'){
      if(distance(p,f.moon.position)<f.moon.capture){this.checkpoint(12);this.cutscene={type:'capture',time:0,duration:3};this.message('달의 중력이 테이블을 끌어당겨요.');}
    }else if(this.map==='Moon'){
      if(this.quest===0&&p.y<1.5){this.quest=1;this.checkpoint(13);}
      const v=this.sim.board.linvel();if(p.y>2&&v.y<-3)this.sim.board.setLinvel({...v,y:-3},true);
    }else if(this.map==='Moon_CozyKitchen'){
      if(this.kitchenAlign>0){this.kitchenAlign-=dt;this.sim.alignTarget={x:0,z:4};}else this.sim.alignTarget=null;
      const center=f.sockets.position;
      this.socketMask=0;this.sim.legs.forEach((l,i)=>{const t=this.sim.foot(l),x=i%2?.58:-.58,z=i<2?3.42:4.58;if(Math.hypot(t.x-x,t.z-z)<.28&&Math.abs(t.y)<.15)this.socketMask|=1<<i;});
      this.timer=this.socketMask===15?this.timer+dt:0;
      if(this.timer>.65){this.sim.setMode('normal');this.cutscene={type:'ending',time:0,duration:82};this.message('의자와 테이블, 따뜻한 차와 친구들. 모두 집에 왔어요.');}
    }else if(this.map==='Soccer'){
      this.matchTime-=dt;this.goalDelay=Math.max(0,this.goalDelay-dt);const b=this.sim.objects.get('football').body,bp=b.translation();
      if(!this.goalDelay&&Math.abs(bp.z)>16&&Math.abs(bp.x)<3){const team=bp.z>0?0:1;this.score[team]++;this.resetBall();this.goalDelay=1.5;this.message(team===0?'GOAL! 득점했어요.':'반대 골대로 들어갔어요.');}
      if(bp.y<-3||Math.abs(bp.z)>20)this.resetBall();
      if(this.matchTime<=0||this.score[0]>=3)this.ending=true;
    }else if(this.map==='Race'){
      this.raceTime+=dt;
      if(this.raceTime>=0&&horizontal(p,f['race-'+this.raceGate].position)<2){this.raceGate++;this.message(`게이트 ${this.raceGate} / 4 통과`);if(this.raceGate===4){this.ending=true;this.result=this.raceTime;if(!this.save.bestRace||this.result<this.save.bestRace)this.save.bestRace=this.result;this.persist();}}
    }
  }
  moveTable(target){
    const p=this.sim.board.translation(),d={x:target.x-p.x,y:target.y-p.y,z:target.z-p.z};
    for(const body of [this.sim.board,...this.sim.legs.flatMap(l=>[l.body,l.yawBody])]){const v=body.translation();body.setTranslation({x:v.x+d.x,y:v.y+d.y,z:v.z+d.z},true);body.setLinvel({x:0,y:0,z:0},true);body.setAngvel({x:0,y:0,z:0},true);}
  }
  assistDirection(){const target=this.target();if(!target)return null;const p=this.sim.board.translation(),d={x:target[0]-p.x,y:this.sim.mode==='squid'?target[1]-p.y:0,z:target[2]-p.z},r=Math.hypot(d.x,d.y,d.z);return r>.18?{x:d.x/r,y:d.y/r,z:d.z/r}:null;}
}
