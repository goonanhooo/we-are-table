// DeviceOrientation angles use the W3C intrinsic Z-X'-Y'' rotation order.
const radians=degrees=>degrees*Math.PI/180;
const multiply=(a,b)=>({
  w:a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z,
  x:a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,
  y:a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,
  z:a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w
});
const conjugate=q=>({w:q.w,x:-q.x,y:-q.y,z:-q.z});
const normalize=q=>{const length=Math.hypot(q.w,q.x,q.y,q.z);return {w:q.w/length,x:q.x/length,y:q.y/length,z:q.z/length};};
const axis=(name,degrees)=>{const h=radians(degrees)/2;return {w:Math.cos(h),x:name==='x'?Math.sin(h):0,y:name==='y'?Math.sin(h):0,z:name==='z'?Math.sin(h):0};};
export function orientationQuaternion(alpha,beta,gamma){
  return multiply(multiply(axis('z',alpha),axis('x',beta)),axis('y',gamma));
}
// Gravity is measured in phone coordinates. Rotate the current gravity vector
// onto its calibrated value to recover pitch and roll when orientation is absent.
export function gravityRelativeQuaternion(reference,current){
  const a=normalizeVector(reference),b=normalizeVector(current);
  const cross={x:b.y*a.z-b.z*a.y,y:b.z*a.x-b.x*a.z,z:b.x*a.y-b.y*a.x};
  const dot=Math.max(-1,Math.min(1,b.x*a.x+b.y*a.y+b.z*a.z));
  if(dot<-.999999){
    const candidate=Math.abs(b.x)<.9?{x:1,y:0,z:0}:{x:0,y:1,z:0};
    const perpendicular={x:b.y*candidate.z-b.z*candidate.y,y:b.z*candidate.x-b.x*candidate.z,z:b.x*candidate.y-b.y*candidate.x};
    return normalize({w:0,...perpendicular});
  }
  return normalize({w:1+dot,...cross});
}
function normalizeVector(v){const length=Math.hypot(v.x,v.y,v.z)||1;return {x:v.x/length,y:v.y/length,z:v.z/length};}
export function calibratedLegTilt(reference,current){
  const q=multiply(conjugate(reference),current);
  const up=multiply(multiply(q,{w:0,x:0,y:1,z:0}),conjugate(q));
  // Horizontal components of the phone's upright axis; a vertical phone is (0,0).
  const x=Math.max(-1,Math.min(1,up.x)),z=Math.max(-1,Math.min(1,up.z));
  const length=Math.hypot(x,z);
  if(length<1e-5)return {x:0,z:0};
  const angle=Math.acos(Math.max(-1,Math.min(1,up.y)));
  const capped=Math.sin(Math.min(angle,80*Math.PI/180));
  // Unity's leg yaw uses the opposite horizontal handedness from the phone sensor.
  return {x:-x/length*capped,z:z/length*capped};
}

// Twist around the phone's long axis. When held upright, this is the ground-normal yaw.
export function calibratedLegYaw(reference,current){
  const q=multiply(conjugate(reference),current);
  return 2*Math.atan2(q.y,q.w)*180/Math.PI;
}
