// 컨트롤러 UI 미리보기: 진짜 index.html을 여러 폰 크기 iframe으로 띄우고(?preview → preview-mock.mjs가 가짜 게임 연결),
// 위 버튼으로 능력·다리·연결 상태를 바꿔 보낸다. 파일이 바뀌면 자동으로 다시 불러온다(로컬에서 고치며 보기용).
const $ = s => document.querySelector(s);
const MODES = [['normal', '기본'], ['giraffe', '기린'], ['tennis', '테니스공'], ['ice', '눈사람'], ['squid', '우주오징어']];
const LEGS = ['노랑', '파랑', '초록', '빨강'];
const CONTROLS = [['keys', '기본'], ['gyro', '자이로'], ['stick', '조이스틱']];
const FRAMES = [
  {id: 'p375', label: '보통 폰 375×812', w: 375, h: 812},
  {id: 'p320', label: '작은 폰 320×568', w: 320, h: 568},
  {id: 'p390', label: '큰 폰 390×844', w: 390, h: 844},
  {id: 'land', label: '가로 844×390', w: 844, h: 390},
  {id: 'entry', label: '입장 화면 375×812', w: 375, h: 812, entry: true},
];
const BIT_NAMES = {normal: {}, giraffe: {16: '늘이기', 32: '줄이기'}, tennis: {64: '접착', 128: '해제'}, ice: {16: '얼음', 32: '풀기'}, squid: {16: '분출'}};
const state = {mode: 'normal', ctl: 'keys', pick: false, leg: 0, busy: [false, false, false, false], unity: true, iceFrozen: true, zoom: 1};

function chips(container, items, pick) {
  for (const [value, label] of items) {
    const b = document.createElement('button'); b.type = 'button'; b.textContent = label; b.dataset.value = value;
    b.onclick = () => pick(value, b); container.append(b);
  }
}
const mark = (container, test) => container.querySelectorAll('button').forEach(b => b.classList.toggle('on', test(b)));
chips($('#modes'), MODES, v => { state.mode = v; mark($('#modes'), b => b.dataset.value === v); push(); });
chips($('#ctl'), CONTROLS, v => { state.ctl = v; mark($('#ctl'), b => b.dataset.value === v); push(); });
chips($('#legs'), LEGS.map((n, i) => [String(i), n]), v => { state.leg = Number(v); state.busy[state.leg] = false; refreshChips(); push(); });
chips($('#busy'), LEGS.map((n, i) => [String(i), n]), v => { const i = Number(v); if (i !== state.leg) state.busy[i] = !state.busy[i]; refreshChips(); push(); });
document.querySelectorAll('[data-ice]').forEach(b => b.onclick = () => { state.iceFrozen = b.dataset.ice === 'frozen'; mark(b.parentElement, x => x === b); push(); });
document.querySelectorAll('[data-pick]').forEach(b => b.onclick = () => { state.pick = b.dataset.pick === '1'; mark(b.parentElement, x => x === b); push(); });
document.querySelectorAll('[data-unity]').forEach(b => b.onclick = () => { state.unity = b.dataset.unity === '1'; mark(b.parentElement, x => x === b); push(); });
document.querySelectorAll('[data-zoom]').forEach(b => b.onclick = () => { state.zoom = Number(b.dataset.zoom); mark(b.parentElement, x => x === b && x.dataset.zoom); layout(); });
function refreshChips() {
  mark($('#modes'), b => b.dataset.value === state.mode);
  mark($('#ctl'), b => b.dataset.value === state.ctl);
  mark($('#legs'), b => Number(b.dataset.value) === state.leg);
  $('#busy').querySelectorAll('button').forEach(b => { const i = Number(b.dataset.value); b.classList.toggle('on', state.busy[i]); b.disabled = i === state.leg; });
}

const cards = [];
function src(f) {
  const q = new URLSearchParams({preview: '1', id: f.id, mode: state.mode, ctl: state.ctl, pick: state.pick ? '1' : '0', leg: String(state.leg), busy: state.busy.map(b => b ? '1' : '0').join(''), unity: state.unity ? '1' : '0', ice: state.iceFrozen ? 'frozen' : 'thawed'});
  if (f.entry) q.set('screen', 'entry');
  return '/index.html?' + q;
}
function build() {
  const root = $('#frames'); root.textContent = ''; cards.length = 0;
  for (const f of FRAMES) {
    const card = document.createElement('figure'); card.className = 'card';
    const cap = document.createElement('figcaption'); cap.textContent = f.label;
    const box = document.createElement('div'); box.className = 'box';
    const frame = document.createElement('iframe'); frame.title = f.label; frame.src = src(f);
    const log = document.createElement('output'); log.className = 'log'; log.textContent = f.entry ? '입장 전 화면' : '입력 없음';
    box.append(frame); card.append(cap, box, log); root.append(card);
    cards.push({f, frame, box, log});
  }
  layout();
}
function layout() {
  for (const {f, frame, box} of cards) {
    frame.style.width = f.w + 'px'; frame.style.height = f.h + 'px';
    frame.style.transform = `scale(${state.zoom})`;
    box.style.width = f.w * state.zoom + 'px'; box.style.height = f.h * state.zoom + 'px';
  }
}
function push() {
  for (const {f, frame} of cards) {
    if (f.entry) continue;
    frame.contentWindow?.postMessage({source: 'wat-preview-control', mode: state.mode, ctl: state.ctl, pick: state.pick, leg: state.leg, busy: state.busy, unity: state.unity, iceFrozen: state.iceFrozen}, location.origin);
  }
}

// 각 화면이 보낸 입력을 사람이 읽을 수 있게 표시
const DIRS = {1: '↑', 2: '↓', 4: '←', 8: '→'};
addEventListener('message', event => {
  if (event.origin !== location.origin || event.data?.source !== 'wat-preview') return;
  const card = cards.find(c => c.f.id === event.data.id); if (!card) return;
  const d = event.data;
  if (d.type === 'input') {
    const names = []; for (const bit of [1, 2, 4, 8]) if (d.bits & bit) names.push(DIRS[bit]);
    for (const [bit, name] of Object.entries(BIT_NAMES[d.mode] || {})) if (d.bits & Number(bit)) names.push(name);
    if (names.length) card.last = names.join(' ');
    card.log.textContent = names.length ? '누르는 중: ' + card.last : card.last ? '입력 없음 (마지막: ' + card.last + ')' : '입력 없음';
  }
  if (d.type === 'leg') { state.leg = d.leg; state.busy[d.leg] = false; refreshChips(); card.log.textContent = LEGS[d.leg] + ' 다리로 바꿈'; push(); }
  if (d.type === 'pose') card.log.textContent = d.active ? `기울기: 앞뒤 ${d.z.toFixed(2)} 좌우 ${d.x.toFixed(2)}` : '기울기 멈춤';
  if (d.type === 'name') card.log.textContent = '닉네임: ' + (d.name || '(없음)');
});

// 로컬에서 파일을 고치면 자동으로 다시 불러오기(같은 내용이면 아무 일도 안 함)
const WATCH = ['/index.html', '/style.css', '/controller.mjs', '/orientation.mjs', '/preview-mock.mjs'];
let lastSignature = '';
async function signature() {
  const parts = await Promise.all(WATCH.map(async path => { try { const text = await (await fetch(path, {cache: 'no-store'})).text(); return path + ':' + text.length + ':' + hash(text); } catch { return path + ':x'; } }));
  return parts.join('|');
}
function hash(text) { let h = 0; for (let i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) | 0; return h; }
async function watch() {
  if ($('#auto').checked && document.visibilityState === 'visible') {
    const sig = await signature();
    if (lastSignature && sig !== lastSignature) build();
    lastSignature = sig;
  }
  setTimeout(watch, 1500);
}
$('#reload').onclick = build;
refreshChips(); build(); watch();
