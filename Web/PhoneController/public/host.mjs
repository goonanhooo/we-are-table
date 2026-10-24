const $=selector=>document.querySelector(selector);
const labels=['노랑','파랑','초록','빨강'];
let session,ws,retry;
function render(data){
  if(data.codes)session={...session,...data};
  $('#unity-status').classList.toggle('online',!!data.unity);
  $('#unity-status').setAttribute('aria-label',data.unity?'게임 연결됨':'게임 연결 대기');
  if(data.origin)$('#public-origin').value=data.origin;
  $('#pc-invite').hidden=!session.nativeRoomCode;
  $('#pc-code').textContent=session.nativeRoomCode||'';
  $('#pc-copy').onclick=async()=>{try{await navigator.clipboard.writeText(session.nativeRoomCode);$('#host-message').textContent='복사됨';}catch{$('#host-message').textContent=session.nativeRoomCode;}};
  const phoneUrl=session.origin?`${session.origin}/#code=${session.joinCode}`:'';
  $('#phone-code').textContent=session.joinCode?`${session.joinCode.slice(0,4)} ${session.joinCode.slice(4)}`:'';
  $('#phone-qr').hidden=!phoneUrl;if(phoneUrl)$('#phone-qr').src=`/api/qr?text=${encodeURIComponent(phoneUrl)}`;
  $('#phone-copy').disabled=!phoneUrl;$('#phone-copy').onclick=async()=>{try{await navigator.clipboard.writeText(phoneUrl);$('#host-message').textContent='복사됨';}catch{$('#host-message').textContent=phoneUrl;}};
  const watchUrl=session.origin?`${session.origin}/watch#code=${session.watchCode}`:'';
  $('#watch-code').textContent=session.watchCode?`${session.watchCode.slice(0,4)} ${session.watchCode.slice(4)}`:'';
  $('#watch-link').href=watchUrl||'#';$('#watch-link').hidden=!watchUrl;
  $('#watch-copy').disabled=!watchUrl;$('#watch-copy').onclick=async()=>{try{await navigator.clipboard.writeText(watchUrl);$('#host-message').textContent='복사됨';}catch{$('#host-message').textContent=watchUrl;}};
  $('#viewer-count').textContent=`관전 ${data.viewers||0}`;
  const slots=$('#host-slots');slots.replaceChildren();
  for(let i=0;i<4;i++){
    const card=document.createElement('article');card.className='host-slot';card.dataset.slot=i;
    const label=document.createElement('div');label.className='leg-label';label.textContent=labels[i];
    const state=document.createElement('span');state.className='slot-state';state.textContent=data.nativeSlots?.[i]?'PC 사용 중':data.occupiedSlots?.[i]?'폰 연결됨':'빈 다리';
    card.append(label,state);slots.append(card);
  }
}
async function init(){
  clearTimeout(retry);
  try{
    const response=await fetch('/api/session');if(!response.ok)throw Error('호스트 접속 오류');
    session=await response.json();render(session);
    if(ws&&ws.readyState<2)ws.close();
    const live=new WebSocket(`${location.protocol==='https:'?'wss:':'ws:'}//${location.host}/ws`);ws=live;
    live.onopen=()=>live.send(JSON.stringify({type:'hello',role:'host',room:session.room,key:session.hostKey}));
    live.onmessage=event=>{const data=JSON.parse(event.data);if(data.type==='lobby')render(data);};
    live.onclose=()=>{if(ws===live)retry=setTimeout(init,1200);};
  }catch{retry=setTimeout(init,2000);}
}
$('#save-origin').onclick=async()=>{
  try{const response=await fetch('/api/public-origin',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({origin:$('#public-origin').value.trim()})});if(!response.ok)throw Error();$('#host-message').textContent='적용됨';}
  catch{$('#host-message').textContent='주소 오류';}
};
init();
