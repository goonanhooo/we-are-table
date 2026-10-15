export const MAPS=[
  {id:'Hallway',name:'하얀 복도',chapter:'01',description:'의자를 따라가다, 바닥 너머의 세상으로.',sky:'#e6ece5',ground:'#e7e7dc',gravity:-9.81,entry:0},
  {id:'Jungle',name:'정글',chapter:'02',description:'기린을 만나고 용암 협곡을 건너세요.',sky:'#a7d9c5',ground:'#91ad68',gravity:-9.81,entry:1},
  {id:'IceCavern',name:'동굴과 눈사람',chapter:'03',description:'테니스공, 두 광차, 눈사람, 얼음 길과 로켓.',sky:'#283b53',ground:'#8b9fac',gravity:-9.81,entry:5},
  {id:'Space',name:'우주',chapter:'04',description:'다리를 조준하고 분사해 달까지 날아가세요.',sky:'#0b1029',ground:'#596176',gravity:0,entry:11},
  {id:'Moon',name:'달',chapter:'05',description:'낮은 중력 속에서 의자가 기다리는 집으로.',sky:'#10162b',ground:'#c1c1bc',gravity:-1.62,entry:13},
  {id:'Moon_CozyKitchen',name:'집 · 따뜻한 부엌',chapter:'06',description:'네 발을 제자리에 놓고 함께 쉬어가세요.',sky:'#dac9a9',ground:'#c8b797',gravity:-1.62,entry:14},
  {id:'Soccer',name:'축구 연습',chapter:'PLAY',description:'발로 공을 밀어 골을 넣는 싱글 연습.',sky:'#b8dcd8',ground:'#7ba66a',gravity:-9.81,entry:null},
  {id:'Race',name:'경주 · 타임어택',chapter:'PLAY',description:'반환점을 순서대로 지나 한 바퀴 완주.',sky:'#c9daca',ground:'#c0bdaa',gravity:-9.81,entry:null},
  {id:'FlatGround',name:'평지 실험실',chapter:'LAB',description:'모든 능력을 자유롭게 연습하세요.',sky:'#d6dfcc',ground:'#bdcaaa',gravity:-9.81,entry:null},
];
export const CHECKPOINTS=[
  ['Hallway','처음 시작 · 복도'],['Jungle','정글에 도착'],['Jungle','기린의 힘 획득'],['Jungle','용암을 건넌 뒤'],['Jungle','스위치 앞'],
  ['IceCavern','동굴에 도착'],['IceCavern','테니스공의 힘 획득'],['IceCavern','첫 광차'],['IceCavern','눈사람 동굴에 도착'],['IceCavern','눈사람의 힘 획득'],['IceCavern','먼 플랫폼에 도착'],
  ['Space','우주오징어의 힘 획득'],['Space','달에 접근'],['Moon','달에서 시작'],['Moon_CozyKitchen','집에 도착'],['Moon_CozyKitchen','끝 · 다시 보기']
];
export const POWERS=[{id:'giraffe',name:'기린',key:'2',bit:1,color:'#d8ad4d'},{id:'tennis',name:'테니스공',key:'3',bit:2,color:'#b4d37a'},{id:'ice',name:'얼음',key:'4',bit:4,color:'#79c9df'},{id:'squid',name:'오징어',key:'5',bit:8,color:'#b68bd6'}];
export function buildMap(id,flags={}){
  const map=MAPS.find(m=>m.id===id)||MAPS[0];
  const spec={...map,ground:false,groundColor:map.ground,spawn:{x:0,z:0,floor:0},boxes:[],objects:[],decor:[],features:{},moon:id==='Moon'||id==='Moon_CozyKitchen'};
  const box=(id,position,size,color,rotation)=>{const b={id,position,size,color:color||map.ground};if(rotation)b.rotation=rotation;spec.boxes.push(b);return b;};
  const floor=(id,x,z,width,depth,y=0,color)=>box(id,[x,y-.25,z],[width,.5,depth],color);
  const detail=(type,id,position,data={})=>spec.decor.push({type,id,position,...data});
  const feature=(id,position,data={})=>spec.features[id]={position,...data};
  if(id==='Hallway'){
    spec.spawn.z=-6;floor('hall-before',0,-1,6,14);floor('hall-after',0,10,6,4);
    if(!flags.trap)floor('trap',0,7,6,2,'0','#ecebe1');
    box('wall-left',[-3.35,1.5,2],[.7,3,24],'#e4e6da');box('wall-right',[3.35,1.5,2],[.7,3,24],'#e4e6da');box('wall-back',[0,1.5,-8.5],[6,3,.5],'#e4e6da');
    detail('chair','chair',[0,0,10]);detail('arch','hall-end',[0,0,11],{color:'#c0c8b7'});feature('trap',[0,0,7]);
  }else if(id==='Jungle'){
    spec.spawn.z=-3;floor('near-bank',0,0,24,13);floor('far-bank',0,12.5,24,8);floor('lava-bottom',0,7.5,24,2,-2.4,'#673127');
    floor('exit-left',-6.75,18,10.5,3);floor('exit-right',6.75,18,10.5,3);floor('exit-back',0,20.25,24,1.5);
    // Stepping rocks have actual colliders; long legs can span the spaces between them.
    for(let i=0;i<3;i++)box('river-rock-'+i,[(i-1)*1.8,-.30,7.5],[i===1?1.4:.75,.6,1.4],'#5e6f55');
    if(!flags.gate)floor('jungle-lid',0,18,3,3,0,'#677a55');
    feature('cage',[0,0,1]);feature('river',[0,-.85,7.5]);feature('crossed',[0,0,10.6]);feature('gate',[0,0,14]);feature('exit',[0,0,18]);
    detail('giraffe','giraffe',[0,0,2.6]);detail('cage','cage',[0,0,2.6],{opened:!!flags.giraffe});
    box('cage-button',[0,flags.giraffe?.002:.06,1],[1.1,flags.giraffe?.004:.12,1.1],flags.giraffe?'#92b66d':'#ca806a');
    for(const [i,x] of [[0,-.58],[1,.58]]){box('gate-pad-'+i,[x,flags.gate?.002:.06,14.58],[.7,flags.gate?.004:.12,.7],flags.gate?'#91b77b':'#bf7365');}
    detail('lava','river-glow',[0,-.85,7.5],{size:[24,.08,2]});detail('arch','jungle-door',[0,0,18.7],{color:'#778869'});
    for(let i=0;i<16;i++){const side=i%2?-1:1;detail('tree','tree-'+i,[side*(5+(i%4)*1.5),0,-3+Math.floor(i/2)*3],{scale:1+(i%3)*.25});}
    detail('volcano','volcano',[14,0,15]);detail('water','ocean',[0,-3,0],{size:[120,120]});
  }else if(id==='IceCavern'){
    spec.spawn.z=-5;floor('cave-entry',0,-5.7,7,8.4);floor('station',0,8,7,2,1.1);floor('snow-cave',0,22,10,10,3);
    const slope=Math.atan(3/10);box('ice-slide',[0,1.5,32],[7,.45,Math.hypot(10,3)],'#a3d7e4',{x:Math.sin(slope/2),y:0,z:0,w:Math.cos(slope/2)});
    floor('landing',0,42,18,10,0,'#d1dcdf');
    spec.objects.push({id:'cart-a',position:flags['cart-a']?[0,.9,7.5]:[0,-.2,0],size:[3.1,.4,3.0],kinematic:true,color:'#5b6b75'},{id:'cart-b',position:flags['cart-b']?[0,2.8,17.5]:[0,.9,10],size:[3.1,.4,3.0],kinematic:true,color:'#5b6b75'},
      {id:'snow-head',position:flags.snowman?[0,4.95,21.5]:[1.4,3.48,20],radius:.42,mass:.55,color:'#f0f3e9',kinematic:!!flags.snowman});
    box('snowman-base',[0,3.6,21.5],[1.15,1.2,1.15],'#e4ebdf');
    feature('caterpillar',[-2,0,-4]);feature('cart-a',[0,0,0]);feature('cart-b',[0,1.1,10]);feature('snowman',[0,3,21.5]);feature('head-target',[0,4.95,21.5]);feature('slide',[0,3,26]);feature('landing',[0,0,38]);feature('rocket',[0,0,40]);
    detail('caterpillar','caterpillar',[-2,0,-4]);detail('snowman','snowman',[0,3,21.5],{assembled:!!flags.snowman});detail('rocket','rocket',[0,0,43]);
    detail('rail','rail-a',[0,0,0],{end:[0,1.1,7.5]});detail('rail','rail-b',[0,1.1,10],{end:[0,3,17.5]});
    for(let i=0;i<14;i++){const side=i%2?-1:1;detail('rock','cave-rock-'+i,[side*(4.5+(i%3)),i<6?-3:2,Math.floor(i/2)*6-6],{scale:2.2+(i%4),color:i>7?'#bfced4':'#3a5264'});}
    for(let i=0;i<8;i++)detail('crystal','crystal-'+i,[(i%2?1:-1)*3.4,i<4?0:3,-4+i*5],{color:i%2?'#7edbe5':'#afa0e3'});
  }else if(id==='Space'){
    spec.spawn={x:0,z:0,floor:2};feature('moon',[0,6,45],{radius:10,capture:18});feature('squid',[0,3,4]);
    detail('moon','moon',[0,6,45],{scale:10});detail('squid','squid',[3,3,4]);detail('stars','stars',[0,0,0]);detail('chair','chair-ship',[-5,3,10],{scale:1.2});
    for(let i=0;i<14;i++)detail('rock','asteroid-'+i,[(i%2?1:-1)*(6+i%5),4+(i%3)*3,i*3],{scale:.5+(i%4)*.4,color:'#737584'});
  }else if(id==='Moon'){
    spec.spawn={x:0,z:-6,floor:5};floor('moon-ground',0,0,80,80,0,'#c1c1bc');feature('home',[0,0,12]);detail('house','home',[0,0,15]);detail('stars','stars',[0,0,0]);
    for(let i=0;i<16;i++)detail('crater','crater-'+i,[(i%2?1:-1)*(4+(i%5)*3),.02,-4+Math.floor(i/2)*4],{scale:.8+(i%3)*.5});
  }else if(id==='Moon_CozyKitchen'){
    spec.spawn.z=-3;floor('kitchen-floor',0,2,9,14,0,'#c8b797');box('kitchen-left',[-4.65,1.6,2],[.3,3.2,14],'#e9dbbd');box('kitchen-back',[0,1.6,9],[9,3.2,.3],'#e9dbbd');
    feature('sockets',[0,0,4]);detail('chair','kitchen-chair',[0,0,5.6]);detail('kitchen','kitchen',[-3.2,0,5]);
    for(let i=0;i<4;i++){const x=i%2?.58:-.58,z=i<2?3.42:4.58;detail('socket','socket-'+i,[x,.012,z],{color:'#91a37a'});}
    detail('window','kitchen-window',[0,1.7,8.78]);detail('giraffe','friend-giraffe',[-2.4,0,6.8],{scale:.55});detail('snowman','friend-snowman',[2.4,0,6.8],{scale:.6,assembled:true});detail('squid','friend-squid',[2.7,1,4.8],{scale:.5});
  }else if(id==='Soccer'){
    spec.spawn.z=-6;floor('football-field',0,0,20,34,0,'#7ba66a');box('field-left',[-10.25,1,0],[.5,2,36]);box('field-right',[10.25,1,0],[.5,2,36]);
    for(const z of [-17,17])for(const x of [-6.5,6.5])box('goal-wall-'+x+'-'+z,[x,1,z],[7,2,.5]);
    spec.objects.push({id:'football',position:[0,.48,0],radius:.45,mass:.55,restitution:.5,color:'#f5f5e9'});
    detail('goal','goal-front',[0,0,16],{color:'#789cce'});detail('goal','goal-back',[0,0,-16],{color:'#d29a86'});detail('pitch','pitch',[0,.006,0]);feature('football',[0,0,0]);
  }else if(id==='Race'){
    spec.spawn={x:8,z:-5,floor:0};floor('race-floor',0,0,26,24,0,'#c0bdaa');box('race-center',[0,.6,0],[11,1.2,10],'#88a276');
    for(const x of [-13.3,13.3])box('race-wall-x'+x,[x,.45,0],[.6,.9,25]);for(const z of [-12.3,12.3])box('race-wall-z'+z,[0,.45,z],[27,.9,.6]);
    const route=[[8,0,8],[-8,0,8],[-8,0,-8],[8,0,-8]];route.forEach((p,i)=>{feature('race-'+i,p);detail('race-gate','race-gate-'+i,p,{number:i+1,color:i===3?'#ede2c6':'#94b3a1'});});
  }else{
    spec.ground=true;feature('practice',[0,0,5]);detail('giraffe','practice-giraffe',[-5,0,3]);
    spec.objects.push({id:'practice-ball',position:[3,.45,3],radius:.42,mass:.55,color:'#e6eed1'});
    box('practice-wall',[0,1.3,7],[6,2.6,.6],'#acb9a0');box('practice-platform',[-4,.75,7],[3,1.5,3],'#b7c4a8');
  }
  return spec;
}
