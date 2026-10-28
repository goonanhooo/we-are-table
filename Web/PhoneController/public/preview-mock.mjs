// UI 미리보기용 가짜 게임 연결. 주소에 ?preview 가 있을 때만 켜지고, 평소에는 아무것도 하지 않는다.
// controller.mjs보다 먼저 실행되어 WebSocket을 가짜로 바꾸고, 게임 서버가 보내는 joined/status 메시지를 흉내 낸다.
// 주소 옵션: mode=normal|giraffe|tennis|ice|squid  leg=0..3  busy=0101(다른 사람이 맡은 다리)  unity=0|1  ice=frozen|thawed  screen=entry|play  ctl=keys|gyro|stick(조작 방식)  pick=1(게임이 다리 고르는 중)
// 미리보기 화면(preview.html)이 postMessage로 상태를 바꾸면 그대로 다시 보낸다. 폰이 보내는 입력은 부모 창에 알려 준다.
const params = new URLSearchParams(location.search);
if (params.has('preview')) {
  const state = {
    mode: params.get('mode') || 'normal',
    leg: Math.max(0, Math.min(3, Number(params.get('leg') ?? 0) || 0)),
    busy: (params.get('busy') || '0000').padEnd(4, '0').slice(0, 4).split('').map(c => c === '1'),
    unity: params.get('unity') !== '0',
    iceFrozen: params.get('ice') !== 'thawed',
    ctl: params.get('ctl') || 'keys',
    pick: params.get('pick') === '1',
  };
  // 조작 방식 버튼을 대신 눌러 준다(입장한 뒤에만 의미가 있음)
  const pickControl = () => setTimeout(() => document.querySelector(`#modes [data-mode="${state.ctl}"]`)?.click(), 60);
  let socket = null;
  const occupied = () => state.busy.map((b, i) => b && i !== state.leg);
  const status = () => ({type: 'status', unity: state.unity, occupiedSlots: occupied(), players: occupied().map(b => b ? {name: '친구', active: false} : null), visual: {mode: state.mode, iceFrozen: state.iceFrozen, legSelect: state.pick}});
  const report = value => { try { parent.postMessage({source: 'wat-preview', id: params.get('id') || '', ...value}, location.origin); } catch {} };

  class PreviewSocket {
    static CONNECTING = 0; static OPEN = 1; static CLOSING = 2; static CLOSED = 3;
    constructor(url) {
      this.url = url; this.readyState = 0; socket = this;
      setTimeout(() => { this.readyState = 1; this.onopen?.(); }, 30);
    }
    receive(value) { if (this.readyState === 1) this.onmessage?.({data: JSON.stringify(value)}); }
    send(text) {
      const m = JSON.parse(text);
      if (m.type === 'hello') { this.receive({type: 'joined', slot: state.leg}); this.receive(status()); pickControl(); }
      if (m.type === 'switch' && !occupied()[m.slot]) { state.leg = m.slot; this.receive({type: 'switched', slot: m.slot}); this.receive(status()); report({type: 'leg', leg: m.slot}); }
      if (m.type === 'input') report({type: 'input', bits: m.bits, mode: m.mode});
      if (m.type === 'pose') report({type: 'pose', active: m.active, x: m.x, z: m.z, yaw: m.yaw});
      if (m.type === 'name') report({type: 'name', name: m.name});
    }
    close() { this.readyState = 3; this.onclose?.({code: 1000}); }
  }
  window.WebSocket = PreviewSocket;
  // 바로 '입장한' 화면을 보여 주도록 방 코드를 넣어 둔다(controller.mjs가 #code=…를 읽고 자동 입장).
  if (params.get('screen') !== 'entry') history.replaceState(null, '', location.pathname + location.search + '#code=PREVIEW1');

  addEventListener('message', event => {
    if (event.origin !== location.origin || event.data?.source !== 'wat-preview-control') return;
    const d = event.data;
    if (typeof d.mode === 'string') state.mode = d.mode;
    if (Number.isInteger(d.leg)) state.leg = d.leg;
    if (Array.isArray(d.busy)) state.busy = d.busy.slice(0, 4).map(Boolean);
    if (typeof d.unity === 'boolean') state.unity = d.unity;
    if (typeof d.iceFrozen === 'boolean') state.iceFrozen = d.iceFrozen;
    if (typeof d.pick === 'boolean') state.pick = d.pick;
    if (typeof d.ctl === 'string' && d.ctl !== state.ctl) { state.ctl = d.ctl; pickControl(); }
    if (socket && Number.isInteger(d.leg)) socket.receive({type: 'switched', slot: state.leg});
    socket?.receive(status());
  });
}
