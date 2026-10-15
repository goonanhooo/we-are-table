import assert from 'node:assert/strict';
import {TablePhysics,PARAMETERS,LEG_LAYOUT,rotate} from '../dist/physics.mjs';
const sim=await TablePhysics.create();
const reports=[];
function run(seconds,keys=[0,0,0,0]){for(let i=0;i<Math.round(seconds/PARAMETERS.step);i++)sim.step(keys);}
function check(name,test,detail){reports.push({name,pass:!!test,detail});assert.ok(test,`${name}: ${JSON.stringify(detail)}`);}
run(2);
let state=sim.state();
check('settles on flat ground',state.y>.68&&state.y<.85&&state.up>.99,state);
check('authored table has 9 bodies and 8 joints',sim.world.bodies.len()===9&&sim.world.impulseJoints.len()===8,{bodies:sim.world.bodies.len(),joints:sim.world.impulseJoints.len()});
for(let slot=0;slot<4;slot++) {
  sim.restart();run(1);
  const keys=[0,0,0,0];keys[slot]=2;run(.28,keys);
  state=sim.state();check(`${LEG_LAYOUT[slot].id} independent swing`,state.legs[slot].swing>15,state.legs.map(l=>l.swing));
  const held=state.legs[slot].swing;run(.45);state=sim.state();
  check(`${LEG_LAYOUT[slot].id} holds released swing`,Math.abs(state.legs[slot].swing-held)<8,{before:held,after:state.legs[slot].swing});
  keys[slot]=4;run(.8,keys);state=sim.state();
  check(`${LEG_LAYOUT[slot].id} yaw spins`,state.legs[slot].spin>70,state.legs[slot]);
  sim.restart();run(.8);keys[slot]=1;run(1.1,keys);state=sim.state();
  check(`${LEG_LAYOUT[slot].id} swing respects ±90°`,Math.abs(state.legs[slot].swing)<=92&&state.legs[slot].swing< -70,state.legs[slot]);
}
sim.restart();run(1);
let before=sim.state();
for(let n=0;n<12;n++) {run(.13,[2,0,0,2]);run(.13,[1,2,2,1]);run(.13,[0,1,1,0]);run(.06);}
state=sim.state();const moved=Math.hypot(state.x-before.x,state.z-before.z);
check('feet drive physical ground locomotion',moved>.15,{moved,state});
sim.recover();run(1);state=sim.state();check('in-place recovery stays upright',state.up>.99&&Math.hypot(state.x,state.z)>moved*.6,state);
sim.restart();run(1);state=sim.state();check('restart returns to origin',Math.hypot(state.x,state.z)<.02&&state.up>.99,state);
// Yaw must remain continuous over more than one full revolution with a raised foot.
run(.6,[2,0,0,0]);run(4,[4,0,0,0]);state=sim.state();
check('raised leg supports continuous 360° spin',Math.abs(state.legs[0].spin)>400,state.legs[0]);
// Stress varied inputs while monitoring the actual constraint anchors.
let seed=71,maxGap=0;
for(let n=0;n<2500;n++) {
  const keys=Array.from({length:4},()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return [0,1,2,4,8,5,10][seed%7];});
  sim.step(keys);
  const p=sim.board.translation();assert.ok([p.x,p.y,p.z].every(Number.isFinite));
  for(const l of sim.legs) {
    const a=l.yawBody.translation(),b=l.body.translation(),q=l.body.rotation();
    const local=rotate(q,{x:0,y:.35,z:0});
    maxGap=Math.max(maxGap,Math.hypot(a.x-b.x-local.x,a.y-b.y-local.y,a.z-b.z-local.z));
  }
}
check('25 seconds of varied input keeps joints connected',maxGap<.05,{maxGap});
sim.free();
console.log(JSON.stringify({passed:reports.length,checks:reports},null,2));
