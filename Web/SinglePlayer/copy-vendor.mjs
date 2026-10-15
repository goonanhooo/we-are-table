import {mkdir,copyFile} from 'node:fs/promises';
await mkdir('dist/vendor',{recursive:true});
for(const [from,to] of [
  ['node_modules/three/build/three.module.min.js','dist/vendor/three.module.js'],
  ['node_modules/three/build/three.core.min.js','dist/vendor/three.core.min.js'],
  ['node_modules/three/LICENSE','dist/vendor/THREE-LICENSE.txt'],
  ['node_modules/@dimforge/rapier3d-compat/dist/rapier.mjs','dist/vendor/rapier.mjs'],
  ['node_modules/@dimforge/rapier3d-compat/LICENSE','dist/vendor/RAPIER-LICENSE.txt'],
])await copyFile(from,to);
console.log('Vendored Three.js and Rapier; no CDN required.');
