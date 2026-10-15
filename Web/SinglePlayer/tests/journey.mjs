import assert from 'node:assert/strict';
import {TablePhysics} from '../dist/physics.mjs';
import {StoryFlow} from '../dist/story.mjs';
import {MAPS,buildMap} from '../dist/maps.mjs';
import {WorldView} from '../dist/world-view.mjs';
import * as T from '../dist/vendor/three.module.js';

const data=new Map(),storage={getItem:k=>data.get(k)||null,setItem:(k,v)=>data.set(k,v)};
const sim=await TablePhysics.create();const flow=new StoryFlow(sim,{storage});const checks=[];
function check(name,condition,detail=''){assert.ok(condition,`${name}: ${JSON.stringify(detail)}`);checks.push({name,pass:true,detail});}
function tick(seconds,{inputs=[0,0,0,0],assist=false,story=true}={}){for(let n=0;n<Math.round(seconds*100);n++){sim.step(inputs,assist&&!flow.locked()?flow.assistDirection():null);if(story)flow.update(.01);}}
function place(x,z,floor=0){sim.reset(x,z,0,floor);tick(.6);}

// Every environment builds renderable geometry and real physics without an infinite fallback floor.
const view=new WorldView(new T.Scene(),null);
for(const map of MAPS){flow.enter(map.id);tick(.6);view.build(flow.spec);view.update(sim,flow,0);check(`${map.id}: physics + scene graph`,sim.world.bodies.len()>=9&&view.group.children.length>5,{bodies:sim.world.bodies.len(),meshes:view.group.children.length});}
view.dispose();

// Real assisted walking opens and falls through the hallway, without a test teleport.
flow.start();let walked=0;while(flow.map==='Hallway'&&walked<7000){tick(.1,{assist:true});walked+=10;}
check('hallway physically walks, trap opens, falls into Jungle',flow.map==='Jungle',{seconds:walked/100});
for(let n=0;n<200&&flow.quest===0;n++){tick(.1,{assist:true});if(n%10===0)flow.interact();}check('assisted cage approach and interaction grant giraffe',flow.quest===1&&(flow.powers&1)&&sim.mode==='giraffe');
tick(8,{inputs:[16,16,16,16]});check('all-leg stretching raises the table',sim.legs.every(l=>l.length>4.8)&&sim.board.translation().y>4.5,sim.state());
let crossed=0;while(flow.quest===1&&crossed<6000){tick(.1,{assist:true});crossed+=10;}
check('extended legs actually cross lava',flow.quest===2&&flow.save.checkpoint===3,{seconds:crossed/100,position:sim.board.translation()});
// Walk from the far bank, shorten legs, and align via the same interaction as the UI.
sim.setMode('normal');for(let n=0;n<300&&flow.quest===2;n++){tick(.1,{assist:true});if(n%10===0)flow.interact();}check('both physical switch contacts open gate',flow.quest===3&&flow.flags.gate&&!sim.terrain.has('jungle-lid'));
for(let n=0;n<300&&flow.map==='Jungle';n++)tick(.1,{assist:true});check('walking through open jungle hole leads into cavern',flow.map==='IceCavern');

place(-1.1,-4);flow.interact();check('caterpillar grants tennis',flow.quest===1&&sim.mode==='tennis'&&(flow.powers&2));
place(0,0);flow.interact();check('boarding creates real cart attachments',flow.cartRide==='cart-a'&&sim.legs.some(l=>l.attachment?.id==='cart-a'));
tick(5.4);check('first moving cart carries table to station',flow.quest===2&&flow.save.checkpoint===7&&sim.board.translation().z>6,{table:sim.board.translation()});
flow.interact();check('cart release is available on arrival',flow.cartRide===null&&sim.legs.every(l=>!l.attachment));
place(0,10,1.1);flow.interact();check('second cart can be boarded',flow.cartRide==='cart-b');
tick(5.4);check('second cart reaches snowman cave',flow.quest===3&&flow.save.checkpoint===8&&sim.board.translation().z>16,sim.board.translation());
// Pick-up constraint test: move the physical head into reach of a foot and use actual grab action.
place(1.4,20,3);sim.setMode('tennis');const foot=sim.foot(sim.legs[0]);const head=sim.objects.get('snow-head').body;
head.setTranslation({x:foot.x,y:foot.y+.40,z:foot.z},true);head.setLinvel({x:0,y:0,z:0},true);
check('tennis foot grabs a dynamic head',sim.grab(0,'snow-head')&&sim.legs[0].attachment.id==='snow-head');
tick(.25,{inputs:[2,0,0,0]});check('grabbed head remains connected while leg swings',Number.isFinite(head.translation().y)&&sim.legs[0].attachment?.id==='snow-head');
sim.releaseAll();head.setTranslation({x:0,y:4.4,z:21.5},true);tick(.02);
check('physical head enters assembly zone and grants ice',flow.quest===4&&flow.flags.snowman&&sim.mode==='ice'&&(flow.powers&4));
// Actual ice friction and gravity carry the table down the inclined collider.
place(0,28,2.7);sim.setMode('ice');tick(12,{assist:true});
check('ice slides down actual slope to landing',flow.quest===5&&flow.save.checkpoint===10,sim.board.translation());
place(0,39,0);flow.interact();check('rocket requires nearby interaction',flow.cutscene?.type==='rocket');tick(8.3);
check('rocket leads to space',flow.map==='Space');tick(2.2);
check('space intro grants squid and zero gravity',sim.mode==='squid'&&(flow.powers&8)&&sim.world.gravity.y===0);
let flights=0;while(flow.map==='Space'&&!flow.cutscene&&flights<3000){tick(.1,{assist:true});flights+=10;}
check('actual leg jets reach moon capture zone',flow.cutscene?.type==='capture',sim.board.translation());tick(3.2);
check('moon capture leads to low-gravity Moon',flow.map==='Moon'&&sim.world.gravity.y===-1.62);tick(4);
check('moon landing checkpoint',flow.save.checkpoint===13&&sim.board.translation().y<2);
place(0,11);flow.interact();check('home entry opens kitchen',flow.map==='Moon_CozyKitchen'&&flow.save.checkpoint===14);
place(0,4);tick(1);check('four real foot sockets begin six-shot ending',flow.socketMask===15&&flow.cutscene?.type==='ending');
flow.interact();tick(.02);check('ending completes full journey and saves checkpoint 15',flow.ending&&flow.save.checkpoint===15);
const restored=new StoryFlow(sim,{storage});restored.resume();check('local save restores final kitchen state',restored.map==='Moon_CozyKitchen'&&restored.powers===15);

flow.enter('Soccer',{practice:true});const ball=sim.objects.get('football').body;ball.setTranslation({x:0,y:.6,z:16.4},true);tick(.02);check('soccer solo goal updates score',flow.score[0]===1);
flow.enter('Race',{practice:true});tick(4.1);place(8,-8);tick(.1);check('race cannot finish before turn gates',!flow.ending&&flow.raceGate===0);
for(const [x,z] of [[8,8],[-8,8],[-8,-8],[8,-8]]){place(x,z);tick(.1);}
check('ordered race gates produce a valid finish and best time',flow.ending&&flow.result>0&&flow.save.bestRace===flow.result);
flow.enter('FlatGround',{practice:true});check('flat lab exposes all abilities without changing campaign progress',flow.powers===15);
const saved=JSON.parse(data.get('we-are-table-single-v2'));check('all 16 story milestones remain reached',Array.from({length:16},(_,i)=>i).every(i=>saved.reached.includes(i)),saved.reached);
sim.free();console.log(JSON.stringify({passed:checks.length,checks},null,2));
