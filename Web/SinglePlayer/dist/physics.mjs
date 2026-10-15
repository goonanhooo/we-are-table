import RAPIER from './vendor/rapier.mjs';

export const PARAMETERS = Object.freeze({
  step: .01, gravity: 9.81, boardSize: [1.4, .1, 1.4], boardMass: 3,
  legSize: [.12, .7, .12], legMass: 1, yawMass: .15, offset: .58,
  swingLimit: Math.PI / 2, swingSpeed: 300 * Math.PI / 180,
  spinSpeed: Math.PI, acceleration: 4000 * Math.PI / 180,
  motorForce: 600, holdForce: 1300, driveDamping: 120,
  holdStiffness: 3000, holdDamping: 80, yawStiffness: 2000, yawDamping: 60,
});
export const LEG_LAYOUT = Object.freeze([
  { name: '왼쪽 뒷다리', id: 'BL', x: -.58, z: -.58, keys: ['KeyW','KeyS','KeyA','KeyD'], labels:['W','S','A','D'], color: '#cd6868' },
  { name: '오른쪽 뒷다리', id: 'BR', x: .58, z: -.58, keys: ['KeyT','KeyG','KeyF','KeyH'], labels:['T','G','F','H'], color: '#59977e' },
  { name: '왼쪽 앞다리', id: 'FL', x: -.58, z: .58, keys: ['KeyI','KeyK','KeyJ','KeyL'], labels:['I','K','J','L'], color: '#d7ae52' },
  { name: '오른쪽 앞다리', id: 'FR', x: .58, z: .58, keys: ['ArrowUp','ArrowDown','ArrowLeft','ArrowRight'], labels:['↑','↓','←','→'], color: '#9783bd' },
]);
const identity = {x:0,y:0,z:0,w:1}, zero = {x:0,y:0,z:0};
export const clamp = (v,min,max) => Math.max(min,Math.min(max,v));
export function rotate(q,v) {
  const tx=2*(q.y*v.z-q.z*v.y),ty=2*(q.z*v.x-q.x*v.z),tz=2*(q.x*v.y-q.y*v.x);
  return {x:v.x+q.w*tx+q.y*tz-q.z*ty,y:v.y+q.w*ty+q.z*tx-q.x*tz,z:v.z+q.w*tz+q.x*ty-q.y*tx};
}
function relativeAngle(parent,child,axis) {
  const a=parent.rotation(),b=child.rotation();
  const component=axis==='x'?a.w*b.x-a.x*b.w-a.y*b.z+a.z*b.y:a.w*b.y+a.x*b.z-a.y*b.w-a.z*b.x;
  const w=a.w*b.w+a.x*b.x+a.y*b.y+a.z*b.z;
  const angle=2*Math.atan2(component,w);
  return Math.atan2(Math.sin(angle),Math.cos(angle));
}
export class TablePhysics {
  static async create() { await RAPIER.init(); return new TablePhysics(); }
  constructor() {this.inputs=[0,0,0,0];this.elapsed=0;this.recoveries=0;this.mode='normal';this.environment=null;this.reset();}
  setEnvironment(environment) {this.environment=environment;const s=environment.spawn||{x:0,z:0,floor:0};this.reset(s.x,s.z,s.heading||0,s.floor||0);}
  reset(x=0,z=0,heading=0,floor=0) {
    this.world?.free();
    const env=this.environment;
    this.world=new RAPIER.World({x:0,y:env?.gravity??-PARAMETERS.gravity,z:0});
    this.world.timestep=PARAMETERS.step;
    this.world.integrationParameters.numSolverIterations=16;
    this.world.integrationParameters.numInternalPgsIterations=4;
    this.world.integrationParameters.maxCcdSubsteps=4;
    this.terrain=new Map();this.objects=new Map();this.anchors=[];this.externalForce=null;this.alignTarget=null;this.stretchOrigin=null;
    if(!env||env.ground)this.addBox({id:'ground',position:[0,-.5,0],size:[400,1,400]});
    for(const box of env?.boxes||[])this.addBox(box);
    for(const obj of env?.objects||[])this.addObject(obj);
    const orientation={x:0,y:Math.sin(heading/2),z:0,w:Math.cos(heading/2)};
    const rootPos={x,y:floor+.90,z};
    const bodyDesc=(pos)=>RAPIER.RigidBodyDesc.dynamic().setTranslation(pos.x,pos.y,pos.z).setRotation(orientation)
      .setAngularDamping(.05).setCanSleep(false).setCcdEnabled(true);
    this.board=this.world.createRigidBody(bodyDesc(rootPos));
    this.boardCollider=this.world.createCollider(RAPIER.ColliderDesc.cuboid(.7,.05,.7).setMass(3).setFriction(1).setCollisionGroups(0x00020005),this.board);
    this.legs=LEG_LAYOUT.map((layout,i)=>{
      const p=rotate(orientation,{x:layout.x,y:-.05,z:layout.z});
      const yawPos={x:x+p.x,y:rootPos.y+p.y,z:z+p.z};
      // A nonzero pivot inertia keeps the two-hinge chain well-conditioned in Rapier.
      const yawDesc=bodyDesc(yawPos).setAdditionalMassProperties(.15,zero,{x:.02,y:.02,z:.02},identity);
      const yawBody=this.world.createRigidBody(yawDesc);
      const centerOffset=rotate(orientation,{x:0,y:-.35,z:0});
      const body=this.world.createRigidBody(bodyDesc({x:yawPos.x+centerOffset.x,y:yawPos.y+centerOffset.y,z:yawPos.z+centerOffset.z}));
      const collider=this.world.createCollider(RAPIER.ColliderDesc.cuboid(.06,.35,.06).setMass(1).setFriction(1.5)
        .setRestitution(0).setCollisionGroups(0x00020005),body);
      const yaw=this.world.createImpulseJoint(RAPIER.JointData.revolute({x:layout.x,y:-.05,z:layout.z},zero,{x:0,y:1,z:0}),this.board,yawBody,true);
      const swing=this.world.createImpulseJoint(RAPIER.JointData.revolute(zero,{x:0,y:.35,z:0},{x:1,y:0,z:0}),yawBody,body,true);
      swing.setLimits(-PARAMETERS.swingLimit,PARAMETERS.swingLimit);
      for(const joint of [yaw,swing]) {joint.setContactsEnabled(false);joint.configureMotorModel(RAPIER.MotorModel.ForceBased);joint.setMotorMaxForce(600);}
      return {i,layout,body,collider,yawBody,yaw,swing,length:.7,attachment:null,holdSwing:0,holdYaw:0,frozen:true,yawFrozen:true,velocity:0,angle:0,spin:0,lastYaw:0,footSupported:false,previousAction:0};
    });
    this.inputs.fill(0);this.holdUntil=this.elapsed+.4;this.supportMemory=0;
    this.distance=0;this.lastPos={x,z};this.heading=heading;
    this.updateMeasurements();
  }
  addBox(spec) {
    const desc=RAPIER.ColliderDesc.cuboid(...spec.size.map(v=>v/2)).setTranslation(...spec.position).setFriction(spec.friction??1.5).setCollisionGroups(0x00010006);
    if(spec.rotation)desc.setRotation(spec.rotation);
    const collider=this.world.createCollider(desc);this.terrain.set(spec.id,{spec,collider});return collider;
  }
  removeBox(id){const box=this.terrain.get(id);if(box){this.world.removeCollider(box.collider,true);this.terrain.delete(id);}}
  addObject(spec) {
    const desc=(spec.kinematic?RAPIER.RigidBodyDesc.kinematicPositionBased():RAPIER.RigidBodyDesc.dynamic()).setTranslation(...spec.position).setCcdEnabled(true);
    const body=this.world.createRigidBody(desc);
    const shape=spec.radius?RAPIER.ColliderDesc.ball(spec.radius):RAPIER.ColliderDesc.cuboid(...spec.size.map(v=>v/2));
    const collider=this.world.createCollider(shape.setMass(spec.mass??.55).setFriction(1).setRestitution(spec.restitution??.1).setCollisionGroups(0x00040007),body);
    const object={spec,body,collider};this.objects.set(spec.id,object);return object;
  }
  freezeObject(id,position){const o=this.objects.get(id);if(!o)return;o.body.setBodyType(RAPIER.RigidBodyType.KinematicPositionBased,true);o.body.setTranslation(position,true);o.body.setNextKinematicTranslation(position);o.body.setLinvel(zero,true);o.body.setAngvel(zero,true);}
  floorAt(x,z,originY=150){
    const ray=new RAPIER.Ray({x,y:originY,z},{x:0,y:-1,z:0});
    const hit=this.world.castRay(ray,300,true,undefined,0x00020005);
    return hit?originY-hit.timeOfImpact:null;
  }
  recover() {const p=this.board.translation(),f=this.floorAt(p.x,p.z);this.recoveries++;this.reset(p.x,p.z,this.heading,f??this.environment?.spawn?.floor??0);}
  restart() {this.recoveries=0;const s=this.environment?.spawn;this.reset(s?.x||0,s?.z||0,s?.heading||0,s?.floor||0);}
  foot(leg){const p=leg.body.translation(),v=rotate(leg.body.rotation(),{x:0,y:-leg.length/2,z:0});return {x:p.x+v.x,y:p.y+v.y,z:p.z+v.z};}
  setMode(mode){
    if(this.mode===mode)return;
    if(this.mode==='tennis')this.releaseAll();
    this.mode=mode;
    for(const col of [this.boardCollider,...this.legs.map(l=>l.collider)]){col.setFriction(mode==='ice'?.02:1.5);col.setFrictionCombineRule(mode==='ice'?RAPIER.CoefficientCombineRule.Min:RAPIER.CoefficientCombineRule.Max);}
  }
  stretch(leg,length){
    const next=clamp(length,.7,4.9),delta=next-leg.length;if(Math.abs(delta)<1e-6)return;
    const p=leg.body.translation(),offset=rotate(leg.body.rotation(),{x:0,y:-delta/2,z:0});
    leg.body.setTranslation({x:p.x+offset.x,y:p.y+offset.y,z:p.z+offset.z},true);
    leg.length=next;leg.collider.setShape(new RAPIER.Cuboid(.06,next/2,.06));leg.collider.setMass(1);leg.swing.setAnchor2({x:0,y:next/2,z:0});
  }
  grab(slot,preferredId=null){
    const leg=this.legs[slot];if(leg.attachment)return true;
    const tip=this.foot(leg);let best=null,distance=.55;
    for(const entry of [...this.objects.values(),...this.terrain.values()]){
      if(preferredId&&entry.spec.id!==preferredId)continue;
      const projected=entry.collider.projectPoint(tip,true);if(!projected)continue;
      const d=Math.hypot(projected.point.x-tip.x,projected.point.y-tip.y,projected.point.z-tip.z);
      if(d<=distance){best={entry,point:projected.point};distance=d;if(entry.body)break;}
    }
    if(!best)return false;
    let body=best.entry.body,temporary=false;
    if(!body){body=this.world.createRigidBody(RAPIER.RigidBodyDesc.fixed().setTranslation(best.point.x,best.point.y,best.point.z));temporary=true;}
    const p=body.translation(),q=body.rotation();const local=rotate({x:-q.x,y:-q.y,z:-q.z,w:q.w},{x:tip.x-p.x,y:tip.y-p.y,z:tip.z-p.z});
    const joint=this.world.createImpulseJoint(RAPIER.JointData.spherical({x:0,y:-leg.length/2,z:0},local),leg.body,body,true);
    leg.attachment={joint,body,temporary,id:best.entry.spec.id};return true;
  }
  release(slot){const leg=this.legs[slot],a=leg.attachment;if(!a)return;this.world.removeImpulseJoint(a.joint,true);if(a.temporary)this.world.removeRigidBody(a.body);leg.attachment=null;}
  releaseAll(){for(const leg of this.legs)this.release(leg.i);}
  updateMeasurements() {
    for(const leg of this.legs) {
      leg.angle=relativeAngle(leg.yawBody,leg.body,'x');
      const raw=relativeAngle(this.board,leg.yawBody,'y');
      leg.spin+=Math.atan2(Math.sin(raw-leg.lastYaw),Math.cos(raw-leg.lastYaw));leg.lastYaw=raw;
      const p=leg.body.translation(),q=leg.body.rotation();
      let lowest=Infinity;
      for(const x of [-.06,.06])for(const z of [-.06,.06])lowest=Math.min(lowest,p.y+rotate(q,{x,y:-leg.length/2,z}).y);
      const tip=this.foot(leg),floor=this.floorAt(tip.x,tip.z,tip.y+.2);
      leg.footSupported=floor!==null&&lowest-floor<.08&&lowest>=floor-.2;
    }
  }
  step(inputs=this.inputs,assist=null) {
    this.inputs=[...inputs];
    const P=PARAMETERS,dt=P.step;
    const groupStretch=this.mode==='giraffe'&&inputs.every(k=>(k&48)!==0&&(k&15)===0);
    if(groupStretch&&!this.stretchOrigin)this.stretchOrigin={...this.board.translation()};if(!groupStretch)this.stretchOrigin=null;
    this.world.gravity={x:0,y:this.mode==='squid'&&!this.environment?.moon?0:this.environment?.gravity??-P.gravity,z:0};
    for(const body of [this.board,...this.legs.flatMap(l=>[l.body,l.yawBody])]){body.resetForces(false);body.resetTorques(false);body.setLinearDamping(this.mode==='squid'?.1:0);}
    for(const leg of this.legs) {
      const input=this.elapsed<this.holdUntil?0:inputs[leg.i];
      leg.collider.setFriction(this.mode==='ice'?.02:assist?.45:1.5);
      leg.collider.setFrictionCombineRule(this.mode==='ice'||assist?RAPIER.CoefficientCombineRule.Min:RAPIER.CoefficientCombineRule.Max);
      if(this.mode==='giraffe')this.stretch(leg,leg.length+(((input&16)?1:0)-((input&32)?1:0))*.56*dt);
      else if(this.mode==='normal'||this.mode==='ice')this.stretch(leg,leg.length-Math.max(0,leg.length-.7)*dt*1.5);
      if(this.mode==='tennis'){
        if((input&16)&&!(leg.previousAction&16))this.grab(leg.i);
        if((input&32)&&!(leg.previousAction&32))this.release(leg.i);
      }
      leg.previousAction=input;
      const swingDir=((input&2)?1:0)-((input&1)?1:0);
      const spinDir=((input&4)?1:0)-((input&8)?1:0);
      const lenScale=.7/leg.length,forceScale=this.mode==='giraffe'?2.6:1,stuck=leg.attachment?2:1;
      if(this.alignTarget||groupStretch){leg.swing.configureMotorPosition(0,P.holdStiffness,P.holdDamping);leg.yaw.configureMotorPosition(0,P.yawStiffness,P.yawDamping);leg.frozen=false;leg.yawFrozen=false;}
      else if(assist&&!(input&15)){
        const q=this.board.rotation(),direction=rotate({x:-q.x,y:-q.y,z:-q.z,w:q.w},assist);
        const turn=Math.atan2(direction.x,direction.z);
        const swingTarget=this.mode==='squid'?Math.atan2(Math.hypot(direction.x,direction.z),Math.max(0,direction.y)):
          this.mode==='giraffe'?(leg.layout.z>0?-.35:.35):Math.sin(this.elapsed*8+(leg.i===0||leg.i===3?0:Math.PI))*.22;
        leg.swing.configureMotorPosition(clamp(swingTarget,-P.swingLimit,P.swingLimit),P.holdStiffness,P.holdDamping);
        leg.yaw.configureMotorPosition(this.mode==='squid'?turn:0,P.yawStiffness,P.yawDamping);leg.frozen=false;leg.yawFrozen=false;
      } else if(swingDir) {
        leg.frozen=false;
        const room=swingDir>0?P.swingLimit-leg.angle:leg.angle+P.swingLimit;
        const speed=swingDir*P.swingSpeed*lenScale*Math.max(.3,clamp(room/(8*Math.PI/180),0,1));
        leg.velocity+=clamp(speed-leg.velocity,-P.acceleration*dt,P.acceleration*dt);
        leg.swing.setMotorMaxForce(P.motorForce*forceScale*stuck);
        leg.swing.configureMotorVelocity(leg.velocity,P.driveDamping*forceScale/lenScale);
      } else {
        if(!leg.frozen) {leg.holdSwing=Math.abs(leg.angle)>86*Math.PI/180?Math.sign(leg.angle)*P.swingLimit:leg.angle;leg.frozen=true;}
        leg.velocity=0;
        leg.swing.setMotorMaxForce(P.holdForce*Math.min(forceScale,1.5)*stuck);
        leg.swing.configureMotorPosition(leg.holdSwing,P.holdStiffness,P.holdDamping);
      }
      if(this.alignTarget||groupStretch||(assist&&!(input&15))){}else if(spinDir) {
        leg.yawFrozen=false;leg.yaw.setMotorMaxForce(P.motorForce);
        leg.yaw.configureMotorVelocity(spinDir*P.spinSpeed*lenScale,P.driveDamping*forceScale/lenScale);
      } else {
        if(!leg.yawFrozen) {leg.holdYaw=leg.lastYaw;leg.yawFrozen=true;}
        leg.yaw.configureMotorPosition(leg.holdYaw,P.yawStiffness,P.yawDamping);
      }
      if(this.mode==='squid'&&((input&16)||assist)){
        const up=rotate(leg.body.rotation(),{x:0,y:1,z:0}),tip=this.foot(leg);
        leg.body.addForceAtPoint({x:up.x*12,y:up.y*12,z:up.z*12},tip,true);
      }
    }
    const supported=this.legs.some(leg=>leg.footSupported);
    this.supportMemory=supported?.1:Math.max(0,this.supportMemory-dt);
    const up=rotate(this.board.rotation(),{x:0,y:1,z:0}).y;
    const rising=this.board.linvel().y-.35;
    if(this.supportMemory>0&&up>.35&&rising>0&&this.mode!=='giraffe')this.board.addForce({x:0,y:-7.6*Math.min(12,rising*3),z:0},true);
    if(assist&&this.mode!=='squid'&&this.supportMemory>0&&!this.legs.some(l=>l.attachment)){
      const speed=2.0,v=this.board.linvel(),strength=7.6;
      this.board.addForce({x:strength*clamp((assist.x*speed-v.x)*6,-18,18),y:0,z:strength*clamp((assist.z*speed-v.z)*6,-18,18)},true);
      // Single-player assistance balances the body with torques; manual leg play remains fully free.
      const q=this.board.rotation(),u=rotate(q,{x:0,y:1,z:0}),f=rotate(q,{x:0,y:0,z:1}),omega=this.board.angvel();
      const delta=Math.atan2(Math.sin(Math.atan2(assist.x,assist.z)-Math.atan2(f.x,f.z)),Math.cos(Math.atan2(assist.x,assist.z)-Math.atan2(f.x,f.z)));
      const ratio=Math.max(1,...this.legs.map(l=>l.length/.7)),balance=220*Math.pow(ratio,1.4);
      this.board.addTorque({x:-u.z*balance-omega.x*32,y:delta*45*ratio*2-omega.y*14*Math.sqrt(ratio)*2,z:u.x*balance-omega.z*32},true);
    }
    if(groupStretch){const u=rotate(this.board.rotation(),{x:0,y:1,z:0}),w=this.board.angvel(),p=this.board.translation(),v=this.board.linvel();this.board.addTorque({x:-u.z*1200-w.x*60,y:-w.y*10,z:u.x*1200-w.z*60},true);this.board.addForce({x:(this.stretchOrigin.x-p.x)*100-v.x*40,y:0,z:(this.stretchOrigin.z-p.z)*100-v.z*40},true);}
    if(this.alignTarget){
      const p=this.board.translation(),v=this.board.linvel(),f=rotate(this.board.rotation(),{x:0,y:0,z:1}),a=Math.atan2(f.x,f.z);
      this.board.addForce({x:7.6*clamp((this.alignTarget.x-p.x)*8-v.x*6,-12,12),y:0,z:7.6*clamp((this.alignTarget.z-p.z)*8-v.z*6,-12,12)},true);
      this.board.addTorque({x:0,y:-a*80-this.board.angvel().y*20,z:0},true);
    }
    if(this.externalForce)this.board.addForce(this.externalForce,true);
    this.world.step();this.elapsed+=dt;this.updateMeasurements();
    const p=this.board.translation();
    this.distance+=Math.hypot(p.x-this.lastPos.x,p.z-this.lastPos.z);this.lastPos={x:p.x,z:p.z};
    if(!this.environment&&(!Number.isFinite(p.y)||p.y < -5||Math.abs(p.x)>190||Math.abs(p.z)>190))this.restart();
  }
  state() {
    const p=this.board.translation(),v=this.board.linvel();
    return {x:p.x,y:p.y,z:p.z,speed:Math.hypot(v.x,v.z),up:rotate(this.board.rotation(),{x:0,y:1,z:0}).y,
      distance:this.distance,recoveries:this.recoveries,mode:this.mode,legs:this.legs.map(leg=>({swing:leg.angle*180/Math.PI,spin:leg.spin*180/Math.PI,grounded:leg.footSupported,keys:this.inputs[leg.i],length:leg.length,attached:leg.attachment?.id||null}))};
  }
  free(){this.world.free();}
}
