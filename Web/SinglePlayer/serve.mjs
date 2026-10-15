import http from 'node:http';
import path from 'node:path';
import {readFile,stat} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
const root=path.join(path.dirname(fileURLToPath(import.meta.url)),'dist');
const mime={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.svg':'image/svg+xml','.wasm':'application/wasm','.json':'application/json','.txt':'text/plain'};
const server=http.createServer(async(req,res)=>{
  try {
    let filename=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const target=path.resolve(root,'.'+filename);
    if(target!==root&&!target.startsWith(root+path.sep)){res.writeHead(403);return res.end('Forbidden');}
    let final=target;if((await stat(target)).isDirectory())final=path.join(target,'index.html');
    const data=await readFile(final);
    res.writeHead(200,{'Content-Type':mime[path.extname(final)]||'application/octet-stream','Cache-Control':'no-store'});res.end(data);
  }catch{res.writeHead(404);res.end('Not found');}
});
server.listen(Number(process.env.PORT)||4173,'127.0.0.1',()=>console.log(`Local: http://127.0.0.1:${server.address().port}`));
