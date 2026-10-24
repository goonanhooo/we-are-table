// Table.prefab dimensions, seen from behind: BL yellow, BR blue, FL green, FR red.
// The colored edges draw through the tabletop just like LegColorXray in the game.
import {mkdir,writeFile} from 'node:fs/promises';
const colors=['#f2bb2b','#428bee','#3caf5c','#e9554e'];
const legs=[[-.58,-.58],[.58,-.58],[-.58,.58],[.58,.58]];
const project=([x,y,z])=>[256+174*(.92*x+.38*z),367+174*(-.88*y-.35*z+.14*x)];
const points=vertices=>vertices.map(p=>project(p).map(n=>n.toFixed(1)).join(',')).join(' ');
const corners=([x,y,z],[a,b,c])=>[[-a,-b,-c],[a,-b,-c],[a,-b,c],[-a,-b,c],[-a,b,-c],[a,b,-c],[a,b,c],[-a,b,c]].map(v=>[v[0]+x,v[1]+y,v[2]+z]);
const boxes=legs.map(([x,z])=>({v:corners([x,.35,z],[.06,.35,.06]),leg:true}));
boxes.push({v:corners([0,.75,0],[.7,.05,.7])});
const faces=[];
for(const box of boxes)for(const [indices,fill] of [[[4,5,6,7],'#c99a73'],[[0,1,5,4],'#a47a57'],[[1,2,6,5],'#8e6648']]){
  const v=indices.map(i=>box.v[i]);faces.push({v,fill,depth:v.reduce((n,[x,y,z])=>n+(.38*x-.3*y-.92*z),0)/4});
}
faces.sort((a,b)=>a.depth-b.depth);
const body=faces.map(f=>`<polygon points="${points(f.v)}" fill="${f.fill}" stroke="${f.fill}" stroke-width=".6" stroke-linejoin="round"/>`).join('');
const edges=[[0,1],[1,2],[2,3],[3,0],[4,5],[5,6],[6,7],[7,4],[0,4],[1,5],[2,6],[3,7]];
const outline=legs.map((_,slot)=>`<g fill="none" stroke="${colors[slot]}" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round">${edges.map(([a,b])=>`<polyline points="${points([boxes[slot].v[a],boxes[slot].v[b]])}"/>`).join('')}</g>`).join('');
const asset=new URL('../public/assets/',import.meta.url);await mkdir(asset,{recursive:true});
for(const colored of [false,true])await writeFile(new URL(`table-start${colored?'-colors':''}.svg`,asset),`<svg xmlns="http://www.w3.org/2000/svg" viewBox="65 145 382 300"><title>Table · yellow back left, blue back right, green front left, red front right</title>${body}${colored?outline:''}</svg>\n`);
