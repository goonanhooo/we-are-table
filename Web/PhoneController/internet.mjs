import {spawn} from 'node:child_process';
import {existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=dirname(fileURLToPath(import.meta.url));
const local=resolve(root,'../../work/cloudflared.exe');
const binary=process.env.CLOUDFLARED||(existsSync(local)?local:'cloudflared');
const port=Number(process.env.PORT||4180);
const child=spawn(binary,['tunnel','--no-autoupdate','--url',`http://127.0.0.1:${port}`],{windowsHide:true,stdio:['ignore','pipe','pipe']});
let found=false,buffer='';
function line(chunk){
  buffer=(buffer+chunk.toString()).slice(-20000);
  const url=buffer.match(/https:\/\/[a-z0-9-]+\.trycloudflare\.com/);
  if(!url||found)return;found=true;
  fetch(`http://127.0.0.1:${port}/api/public-origin`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({origin:url[0]})}).then(async r=>{if(!r.ok)throw Error('relay not ready');console.log(`Internet controller: ${url[0]}`);console.log(`Invite link and QR: http://127.0.0.1:${port}/host`);}).catch(e=>console.error(e.message));
}
child.stdout.on('data',line);child.stderr.on('data',line);
child.on('error',e=>console.error('cloudflared could not start:',e.message));
child.on('exit',code=>{console.log('Internet tunnel stopped:',code);if(!found)console.error(buffer);process.exitCode=code||0;});
for(const sig of ['SIGINT','SIGTERM'])process.on(sig,()=>child.kill());
