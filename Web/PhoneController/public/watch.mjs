const $=selector=>document.querySelector(selector);
const clean=value=>String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'').slice(0,8);
const format=value=>{const code=clean(value);return code.length>4?`${code.slice(0,4)} ${code.slice(4)}`:code;};
let ws,retry,wanted=false,received=false;
function connect(){
  clearTimeout(retry);if(!wanted)return;
  const code=clean($('#watch-input').value);
  if(code.length!==8){$('#watch-state').textContent='코드 8자리';wanted=false;return;}
  const live=new WebSocket(`${location.protocol==='https:'?'wss:':'ws:'}//${location.host}/ws`);ws=live;
  live.onopen=()=>live.send(JSON.stringify({type:'hello',role:'viewer',code}));
  live.onmessage=event=>{
    const data=JSON.parse(event.data);
    if(data.type==='watch-ready'){$('#watch-form').hidden=true;$('#watch-screen').hidden=false;$('#watch-state').textContent='게임 화면 대기';}
    if(data.type==='watch-status'){$('#watch-state').textContent=data.unity?'연결됨':'게임 연결 대기';if(!data.unity){received=false;$('#watch-image').hidden=true;$('#watch-wait').hidden=false;}}
    if(data.type==='frame'){$('#watch-image').src=`data:image/jpeg;base64,${data.data}`;$('#watch-image').hidden=false;$('#watch-wait').hidden=true;received=true;}
  };
  live.onclose=event=>{
    if(ws!==live)return;
    if(event.code===1008){wanted=false;$('#watch-form').hidden=false;$('#watch-screen').hidden=true;$('#watch-state').textContent='관전 코드 오류';return;}
    $('#watch-state').textContent='다시 연결 중';if(wanted)retry=setTimeout(connect,1500);
  };
};
$('#watch-input').addEventListener('input',event=>event.target.value=format(event.target.value));
$('#watch-form').onsubmit=event=>{event.preventDefault();wanted=true;connect();};
const invite=clean(new URLSearchParams(location.hash.slice(1)).get('code'));
if(invite.length===8){$('#watch-input').value=format(invite);wanted=true;connect();}
