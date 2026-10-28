// UI 미리보기용 로컬 서버(설치할 것 없음, Node.js 18+). 게임·중계 서버 없이 public/ 파일만 그대로 보여 준다.
//   npm run preview            → http://localhost:4190/preview
//   PORT=5000 npm run preview  → 포트 바꾸기
// 같은 네트워크의 폰에서 보려면 이 컴퓨터의 IP로 접속(예: http://192.168.0.10:4190/index.html?preview).
import {createServer} from 'node:http';
import {readFile, stat} from 'node:fs/promises';
import {extname, join, normalize} from 'node:path';
import {fileURLToPath} from 'node:url';

const root = fileURLToPath(new URL('../public/', import.meta.url));   // 경로에 공백이 있어도 안전
const port = Number(process.env.PORT || 4190);
const types = {'.html': 'text/html', '.mjs': 'text/javascript', '.js': 'text/javascript', '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml', '.json': 'application/json'};
// 배포 서버(cloudflare/worker.mjs)와 같은 보안 헤더 — 미리보기에서 되는 것이 실제로도 되게
const csp = "default-src 'self'; connect-src 'self' ws: wss:; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'self'";

createServer(async (req, res) => {
  try {
    let path = decodeURIComponent(new URL(req.url, 'http://local').pathname);
    if (path === '/') path = '/preview.html';
    if (!extname(path)) path += '.html';
    const file = normalize(join(root, path));
    if (!file.startsWith(root)) { res.writeHead(403); res.end(); return; }
    await stat(file);
    res.writeHead(200, {'Content-Type': (types[extname(file)] || 'application/octet-stream') + '; charset=utf-8', 'Cache-Control': 'no-store', 'Content-Security-Policy': csp, 'X-Content-Type-Options': 'nosniff'});
    res.end(await readFile(file));
  } catch { res.writeHead(404); res.end('Not found'); }
}).listen(port, () => console.log(`UI 미리보기: http://localhost:${port}/preview`));
