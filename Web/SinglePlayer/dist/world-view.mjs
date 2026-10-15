import * as T from './vendor/three.module.js';

export class WorldView {
  constructor(scene,texture){this.scene=scene;this.texture=texture;this.group=null;this.dynamic=new Map();this.named=new Map();this.owned=[];}
  material(color,extra={}){const m=new T.MeshStandardMaterial({color,roughness:.9,...extra});this.owned.push(m);return m;}
  mesh(group,shape,pos,size,color,extra={}){
    let geometry=shape==='sphere'?new T.SphereGeometry(1,16,12):shape==='cone'?new T.ConeGeometry(1,1,7):shape==='cylinder'?new T.CylinderGeometry(1,1,1,12):new T.BoxGeometry(1,1,1);
    this.owned.push(geometry);const mesh=new T.Mesh(geometry,this.material(color,extra));mesh.position.set(...pos);mesh.scale.set(...size);mesh.castShadow=true;mesh.receiveShadow=true;group.add(mesh);return mesh;
  }
  box(g,p,s,c){return this.mesh(g,'box',p,s,c);}
  sphere(g,p,r,c){return this.mesh(g,'sphere',p,[r,r,r],c);}
  build(spec){
    this.dispose();this.spec=spec;this.group=new T.Group();this.scene.add(this.group);this.dynamic.clear();this.named.clear();
    this.scene.background=new T.Color(spec.sky);this.scene.fog=spec.id==='Space'?null:new T.Fog(spec.sky,spec.id==='Moon'?30:18,95);
    const sun=this.scene.userData.sun;if(sun){sun.color.set(spec.id==='Moon_CozyKitchen'?'#ffcc85':spec.id==='Space'?'#d4d5ff':'#ffebd1');sun.intensity=spec.id==='Space'?1.8:2.8;}
    if(spec.ground){const ground=this.mesh(this.group,'box',[0,-.5,0],[400,1,400],spec.groundColor);ground.castShadow=false;const grid=new T.GridHelper(100,100,'#8e9f7c','#9aab87');grid.position.y=.002;grid.material.transparent=true;grid.material.opacity=.13;this.group.add(grid);this.owned.push(grid.geometry,grid.material);}
    for(const b of spec.boxes){const m=this.box(this.group,b.position,b.size,b.color);if(b.rotation)m.quaternion.copy(b.rotation);this.named.set(b.id,m);}
    for(const o of spec.objects){
      const g=new T.Group();this.group.add(g);
      if(o.id.startsWith('cart')){
        this.box(g,[0,0,0],o.size,'#536572');
        for(const x of [-1.05,1.05])for(const z of [-.9,.9]){const w=this.mesh(g,'cylinder',[x,-.25,z],[.24,.16,.24],'#303b43');w.rotation.z=Math.PI/2;}
        for(const x of [-1.4,1.4])this.box(g,[x,.22,0],[.12,.4,2.8],'#809098');
      }else{
        const m=o.radius?this.sphere(g,[0,0,0],o.radius,o.color):this.box(g,[0,0,0],o.size,o.color);
        if(o.id==='football'){const wire=new T.LineSegments(new T.EdgesGeometry(new T.IcosahedronGeometry(o.radius*1.005,1)),new T.LineBasicMaterial({color:'#58646a'}));g.add(wire);this.owned.push(wire.geometry,wire.material);}
        if(o.id==='snow-head'){for(const x of [-.13,.13])this.sphere(g,[x,.05,-.37],.035,'#455154');const nose=this.mesh(g,'cone',[0,-.02,-.46],[.055,.2,.055],'#d19659');nose.rotation.x=-Math.PI/2;}
      }
      this.dynamic.set(o.id,g);
    }
    for(const d of spec.decor){const g=new T.Group();g.position.set(...d.position);if(d.scale)g.scale.setScalar(d.scale);this.group.add(g);this.named.set(d.id,g);this.decorate(g,d);}
    this.marker=new T.Group();this.group.add(this.marker);
    const cone=this.mesh(this.marker,'cone',[0,0,0],[.12,.32,.12],'#f2de8c',{emissive:'#6e5821',emissiveIntensity:.4});cone.rotation.z=Math.PI;
    const ring=new T.Mesh(new T.RingGeometry(.35,.39,32),new T.MeshBasicMaterial({color:'#f1db86',side:T.DoubleSide,transparent:true,opacity:.8}));ring.rotation.x=-Math.PI/2;ring.position.y=-1;this.marker.add(ring);this.owned.push(ring.geometry,ring.material);
  }
  decorate(g,d){
    const wood='#ae8055',dark='#655445';
    if(d.type==='chair'){
      this.box(g,[0,.48,0],[.75,.1,.75],wood);this.box(g,[0,.87,.32],[.75,.8,.1],wood);
      for(const x of [-.27,.27])for(const z of [-.27,.27])this.box(g,[x,.24,z],[.09,.48,.09],wood);
    }else if(d.type==='giraffe'){
      this.box(g,[0,1.05,0],[.8,.65,1.3],'#d9b36b');for(const x of [-.28,.28])for(const z of [-.45,.45])this.box(g,[x,.5,z],[.12,1,.12],'#bd914d');
      this.box(g,[0,1.85,.4],[.3,1.5,.3],'#d9b36b');this.box(g,[0,2.55,.59],[.5,.34,.65],'#d9b36b');for(const x of [-.24,.24])this.sphere(g,[x,2.6,.75],.055,dark);
      for(let i=0;i<6;i++)this.box(g,[(i%2?1:-1)*.41,1+(i%3)*.16,-.3+(i%4)*.2],[.01,.16,.2],'#95703e');
    }else if(d.type==='cage'){
      for(const x of [-1,1])for(let z=-1;z<=1;z+=.5)this.box(g,[x,1.5,z],[.055,3,.055],'#6b7d78');
      for(const z of [-1,1])for(let x=-1;x<=1;x+=.5)this.box(g,[x,1.5,z],[.055,3,.055],'#6b7d78');this.box(g,[0,3,0],[2.1,.08,2.1],'#6b7d78');if(d.opened)g.position.y+=4;
    }else if(d.type==='tree'){
      this.mesh(g,'cylinder',[0,.7,0],[.14,1.4,.14],'#97815c');for(let i=0;i<3;i++)this.mesh(g,'cone',[0,1.3+i*.45,0],[1.05-i*.22,1.15,1.05-i*.22],i%2?'#648756':'#75965e');
    }else if(d.type==='rock'||d.type==='moon'){
      const geo=new T.IcosahedronGeometry(1,d.type==='moon'?2:0);this.owned.push(geo);const m=new T.Mesh(geo,this.material(d.color||'#c5c3b9'));g.add(m);m.castShadow=true;m.receiveShadow=true;
      if(d.type==='moon')for(let i=0;i<10;i++)this.sphere(g,[Math.sin(i*2.4)*.9,Math.cos(i*2.4)*.5,-.6],.11,'#a09f98');
    }else if(d.type==='crystal'){
      const m=this.mesh(g,'cone',[0,.5,0],[.18,1,.18],d.color,{emissive:d.color,emissiveIntensity:.3});m.rotation.z=.15;
    }else if(d.type==='volcano'){
      this.mesh(g,'cone',[0,3,0],[5,6,5],'#6c7663');this.mesh(g,'cylinder',[0,5.9,0],[.8,.15,.8],'#e6a05a',{emissive:'#cf5724',emissiveIntensity:.9});
    }else if(d.type==='water'||d.type==='lava'){
      if(d.type==='lava')this.box(g,[0,0,0],d.size,'#e17b45');
      else{const m=new T.Mesh(new T.PlaneGeometry(...d.size),this.material('#679f99',{transparent:true,opacity:.8}));m.rotation.x=-Math.PI/2;g.add(m);this.owned.push(m.geometry);}
    }else if(d.type==='arch'){
      for(const x of [-2,2])this.box(g,[x,1.6,0],[.35,3.2,.4],d.color);this.box(g,[0,3.1,0],[4.3,.35,.4],d.color);
    }else if(d.type==='caterpillar'){
      for(let i=0;i<7;i++)this.sphere(g,[i*.23,.23,Math.sin(i*.8)*.1],.2,i%2?'#c6da8d':'#d7e49f');for(const z of [-.09,.09])this.sphere(g,[-.13,.27,z],.025,dark);
    }else if(d.type==='snowman'){
      this.sphere(g,[0,.6,0],.65,'#e9eee6');this.sphere(g,[0,1.25,0],.47,'#e9eee6');
      if(d.assembled&&d.id!=='snowman'){this.sphere(g,[0,1.95,0],.4,'#e9eee6');for(const x of [-.13,.13])this.sphere(g,[x,2,-.36],.03,dark);}
      for(const x of [-1,1]){const arm=this.box(g,[x*.65,1.1,0],[.7,.04,.04],dark);arm.rotation.z=x*.25;}
      for(let i=0;i<3;i++)this.sphere(g,[0,.55+i*.22,-.57],.035,dark);
    }else if(d.type==='rail'){
      const end=d.end.map((v,i)=>v-d.position[i]),length=Math.hypot(end[1],end[2]);
      for(const x of [-.85,.85]){const rail=this.box(g,[x,end[1]/2-.35,end[2]/2],[.08,.08,length],'#697580');rail.rotation.x=-Math.atan2(end[1],end[2]);}
      for(let z=0;z<end[2];z+=.7)this.box(g,[0,-.38+end[1]*z/end[2],z],[2,.1,.2],'#625d54');
    }else if(d.type==='rocket'){
      this.mesh(g,'cylinder',[0,2.3,0],[.8,4.6,.8],'#e5e6dd');this.mesh(g,'cone',[0,5.1,0],[.8,1,.8],'#b98b78');
      this.sphere(g,[0,3,-.72],.31,'#788f9e');for(const x of [-1,1])this.box(g,[x*.78,.5,0],[.35,1.1,1.2],'#aa8070');this.box(g,[0,.05,0],[4,.1,4],'#909995');
      const flame=this.mesh(g,'cone',[0,-1,0],[.65,2,.65],'#efb770',{emissive:'#ec834d',emissiveIntensity:1.2});flame.rotation.z=Math.PI;flame.name='flame';flame.visible=false;
    }else if(d.type==='squid'){
      this.sphere(g,[0,.8,0],.65,'#b9a0d6');for(const x of [-.24,.24]){this.sphere(g,[x,.85,-.5],.14,'#f0eee1');this.sphere(g,[x,.86,-.62],.055,dark);}
      for(let i=0;i<6;i++){const a=i*Math.PI/3;const tentacle=this.box(g,[Math.cos(a)*.4,.1,Math.sin(a)*.4],[.1,.9,.1],'#b9a0d6');tentacle.rotation.z=Math.sin(a)*.25;}
    }else if(d.type==='stars'){
      const points=[];let s=18;for(let i=0;i<1000;i++){s=(Math.imul(s,1664525)+1013904223)>>>0;const a=s/4294967296*6.28;s=(Math.imul(s,1664525)+1013904223)>>>0;const h=s/4294967296*2-1;const r=75,side=Math.sqrt(1-h*h);points.push(Math.sin(a)*side*r,h*r,Math.cos(a)*side*r);}
      const geo=new T.BufferGeometry();geo.setAttribute('position',new T.Float32BufferAttribute(points,3));const mat=new T.PointsMaterial({color:'#f0e9d8',size:.15,sizeAttenuation:true});g.add(new T.Points(geo,mat));this.owned.push(geo,mat);
    }else if(d.type==='crater'){
      const m=new T.Mesh(new T.RingGeometry(.65,1,30),this.material('#a4a69f'));m.rotation.x=-Math.PI/2;g.add(m);this.owned.push(m.geometry);
    }else if(d.type==='house'){
      this.box(g,[0,1.8,0],[7,3.6,6],'#bdb9a6');const roof=this.box(g,[0,3.6,0],[7.6,.3,6.6],'#878a83');roof.rotation.z=.06;
      this.box(g,[0,1,-3.02],[1.5,2,.04],'#796e59');for(const x of [-2.2,2.2])this.box(g,[x,1.8,-3.05],[1.2,1.2,.05],'#efc787');this.box(g,[0,.2,-4],[3,.4,2],'#a6a79c');
    }else if(d.type==='kitchen'){
      this.box(g,[0,.55,0],[1.25,1.1,4.8],'#a38e70');this.box(g,[0,1.12,0],[1.35,.08,4.9],'#e5d8b7');for(let i=0;i<4;i++)this.box(g,[.64,.6,-1.8+i*1.15],[.04,.65,.85],'#cbb796');
      this.mesh(g,'cylinder',[0,1.2,-.8],[.22,.16,.22],'#b8b9a6');this.box(g,[0,1.17,.9],[.9,.03,1.0],'#a5aea4');
    }else if(d.type==='window'){
      this.box(g,[0,0,0],[2.5,1.5,.07],'#697889');for(const x of [-1.25,0,1.25])this.box(g,[x,0,-.04],[.06,1.6,.06],'#ede1c6');for(const y of [-.75,0,.75])this.box(g,[0,y,-.04],[2.6,.06,.06],'#ede1c6');
    }else if(d.type==='socket'){
      const m=new T.Mesh(new T.RingGeometry(.20,.29,32),new T.MeshBasicMaterial({color:d.color,side:T.DoubleSide}));m.rotation.x=-Math.PI/2;g.add(m);this.owned.push(m.geometry,m.material);
    }else if(d.type==='goal'||d.type==='race-gate'){
      const width=d.type==='goal'?6:4;for(const x of [-width/2,width/2])this.box(g,[x,1.25,0],[.10,2.5,.10],d.color);this.box(g,[0,2.5,0],[width,.10,.10],d.color);
      if(d.type==='goal')for(let i=-3;i<=3;i+=.5)this.box(g,[i,1.2,1],[.025,2.4,.025],'#d0dbca');
    }else if(d.type==='pitch'){
      for(const x of [-9,9])this.box(g,[x,0,0],[.05,.01,32],'#dae4cf');for(const z of [-16,0,16])this.box(g,[0,0,z],[18,.01,.05],'#dae4cf');
      const circle=new T.Mesh(new T.RingGeometry(2.45,2.5,60),new T.MeshBasicMaterial({color:'#dae4cf'}));circle.rotation.x=-Math.PI/2;g.add(circle);this.owned.push(circle.geometry,circle.material);
    }
  }
  update(sim,flow,time){
    for(const [id,g] of this.dynamic){const object=sim.objects.get(id);if(object){g.position.copy(object.body.translation());g.quaternion.copy(object.body.rotation());}}
    for(const [id,mesh] of this.named){
      if(sim.terrain.has(id))mesh.visible=true;else if(this.spec.boxes.some(b=>b.id===id))mesh.visible=false;
      if(id==='cage'&&flow.flags.giraffe)mesh.position.y=Math.min(4,mesh.position.y+.03);
      if(id==='cage-button'&&flow.flags.giraffe){mesh.position.y=.002;mesh.scale.y=.004;mesh.material.color.set('#92b66d');}
      if(id.startsWith('gate-pad-')){const i=Number(id.at(-1));mesh.material.color.set(flow.buttons?.[i]||flow.flags.gate?'#88b76e':'#bf7365');if(flow.flags.gate){mesh.position.y=.002;mesh.scale.y=.004;}}
      if(id.startsWith('friend-'))mesh.visible=flow.cutscene?.type==='ending'||flow.ending;
      if(id==='rocket'){const launching=flow.cutscene?.type==='rocket';mesh.position.y=launching?Math.max(0,(flow.cutscene.time-3)*5):0;mesh.getObjectByName('flame').visible=launching&&flow.cutscene.time>3;}
      if(id.startsWith('socket-')){const i=Number(id.at(-1));mesh.children[0].material.color.set(flow.socketMask&(1<<i)?'#dae7a4':'#8da17a');}
    }
    const target=flow.target();this.marker.visible=!!target&&!flow.cutscene&&!flow.ending;
    if(target){this.marker.position.set(target[0],target[1]+1.35+Math.sin(time*.003)*.06,target[2]);}
  }
  dispose(){if(this.group)this.scene.remove(this.group);for(const asset of this.owned)asset.dispose();this.owned=[];this.group=null;}
}
