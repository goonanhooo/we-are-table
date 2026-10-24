# 현재 상태 — 2026-10-16

이 문서는 구현과 검증 범위의 기록입니다. 실행 중인 프로세스와 임시 인터넷 주소는 이후 달라질 수 있습니다.

## 구현된 것

| 구성 | 현재 범위 |
|---|---|
| Unity 원본 게임 | Windows 실행본과 macOS 공용 실행본을 빌드했습니다. 기존 Steam/Unity Relay 방에서 각 PC가 게임 장면을 렌더링하고, 호스트가 테이블 물리를 계산합니다. PC 키보드와 폰이 서로 다른 다리를 한 방에서 조작하도록 연결했습니다. |
| 폰 컨트롤러 (`Web/PhoneController/`) | 공통 QR·폰 입장 코드, 큰 다리 전환 버튼, 능력별 동작 버튼, 게임 상태에 따른 테이블 그림, 색 표시 전환이 있습니다. 아이폰 Safari에서 동작·방향 센서 권한, 수직보정, 기울기·회전 각도 표시와 조작이 작동합니다. 작은 세로 화면에서도 조작 버튼이 한 화면에 보입니다. |
| 웹 관전 (`/watch`) | Unity 호스트가 보내는 게임 화면을 웹에서 볼 수 있습니다. 화면은 최대 960px·초당 최대 8장이고 오디오는 전송하지 않습니다. |
| 독립 웹 싱글 (`Web/SinglePlayer/`) | Unity WebGL 빌드가 아닌 Three.js·Rapier 재구현입니다. 본편 맵 흐름, 축구·경주·평지 실험실과 브라우저 저장을 포함합니다. Unity 방이나 폰 중계와 연결되는 멀티플레이 버전은 아닙니다. |

## 확인한 것

- 웹 싱글의 물리 검사 23개와 여정·맵 검사 40개가 통과했습니다.
- 폰 중계 검사 4개가 통과했습니다. 다리 예약 충돌, 입력·자이로 신호, 재접속을 포함합니다.
- 공개 HTTPS 경로에서 네 폰 클라이언트와 네 웹 관전 클라이언트를 동시에 연결해, 관전자들이 같은 Unity 화면을 받는 것을 검사했습니다. 이는 자동 클라이언트 검사입니다.
- Unity 화면에서 PC가 소유한 다리와 폰이 소유한 다리가 같은 방에 표시되고, 서로의 다리를 중복 선택하지 못하는 것을 확인했습니다.
- 사용자가 실제 아이폰 Safari에서 자이로 작동과 스크롤 없는 컨트롤러 화면을 확인했습니다.

## 아직 확인하거나 준비할 것

- 서로 떨어진 실제 Windows PC 두 대 이상이 같은 Unity 방에 접속해 장면을 동기화하는 끝까지의 플레이 검사는 하지 못했습니다.
- 실제 폰 네 대가 각기 다른 네트워크에서 동시에 플레이하는 검사는 하지 못했습니다. 네 폰 동시 연결은 자동 클라이언트로만 확인했습니다.
- 고정 Cloudflare 서버를 추가했습니다. Git 푸시 후 자동 배포는 아직 구성하지 않았습니다. 이전 로컬 Quick Tunnel 방식도 선택적으로 남아 있습니다.
- Unity 실행 파일과 서버 실행 중 생성되는 호스트 키·방 코드는 Git에 포함하지 않았습니다. 저장소의 소스로 빌드하고 실행해야 합니다.

## 2026-10-16 Cloudflare 고정 서버·Mac 빌드 추가 검증

- [고정 컨트롤러](https://we-are-table-controller.eraser-room-game.workers.dev/)에 화면과 WebSocket 중계를 함께 배포했습니다. 방마다 Durable Object가 분리되며 원래 PC나 임시 터널이 필요하지 않습니다.
- 최신 Unity 호스트는 함께하기에서 방을 만들면 클라우드 폰 방을 자동 생성합니다. 공개 서버 주소만 빌드에 포함하고 호스트 키는 실행 중에 발급합니다.
- 공개 HTTPS/WSS 중계 검사 10개, 실제 Mac QA 게임의 관절·기린·테니스·얼음·오징어·연결 복구 검사 9개가 통과했습니다. 실제 브라우저의 얼음·풀기 버튼도 Mac 상태를 변경했습니다.
- macOS Apple Silicon·Intel 공용 실행본을 빌드했습니다. 빌드 오류 0개, 경고 2개입니다. Steam App ID 480 초기화도 Mac에서 확인했습니다.
- 이번 폰 클라이언트·자이로 입력 검사는 자동 클라이언트와 합성 센서를 사용했습니다. 실제 아이폰 센서, 서로 다른 장소의 Unity PC 동시 플레이와 Steam 출시 인증은 별도 검증이 필요합니다.
- 예전 `trycloudflare.com` 주소는 원래 컴퓨터의 이전 배포로 남아 있습니다. 새 Mac 실행본의 폰 QR은 위 고정 서버로 연결합니다.

### 최종 Mac 배포본 재확인

2026-10-16 배포용 `.app` 자체를 실행해 시작 화면의 렌더링, Steam 초기화, Cloudflare 연결, 공통 QR 및 자동 폰 클라이언트 네 개의 입장을 확인했습니다. 실행 중 예외는 없었습니다. Steam 플러그인의 서명 불일치를 발견해 Mac 빌드 후 테스트용 재서명과 전체 서명 검증을 자동으로 수행하도록 고쳤습니다. 게임 내 메뉴를 직접 클릭하는 검사는 네이티브 앱 조작 서비스 오류로 수행하지 못했습니다.

## 2026-10-16 최신 Windows 빌드

- 최신 원본 소스를 별도 빌드 프로젝트에 동기화해 Windows 64비트 Mono 실행본을 새로 만들었습니다. 활성 씬 9개를 포함하며 빌드 결과는 오류 0개, 경고 2개입니다.
- 기존 경고는 `PerTableMode.OnDestroy`의 메서드 숨김과 Unity Services 프로젝트 연결 안내입니다. Steam 테스트 App ID는 480이며 정식 출시 App ID가 아닙니다.
- 실행 파일·UnityPlayer·Mono·Steam DLL이 모두 AMD64인지 확인했습니다. Steam DLL은 원본 플러그인과 해시가 같습니다.
- 압축된 게임 리소스에서 고정 Cloudflare 주소를 직접 읽어 확인했습니다. QA 전용 드라이버와 비공개 호스트 설정은 배포본에 없습니다.
- ZIP CRC와 포함 파일 205개의 SHA-256을 모두 검사했습니다. macOS에서 Windows용으로 빌드했으며, Windows 실제 실행·실제 여러 PC의 Steam 플레이는 아직 검사하지 않았습니다.
- ZIP 전체를 풀고 Steam을 실행한 뒤 `we-are-table.exe`를 실행합니다. EXE와 `_Data`·`MonoBleedingEdge`·DLL을 함께 유지해야 합니다.

### 현재 컴퓨터의 산출물과 이어받기

- 작업 자료: `/Users/juhyeongpyo/Documents/Codex/2026-09-20/new-chat`
- Windows ZIP: `outputs/windows-phone-build/we-are-table-windows.zip` (149,503,564바이트)
- Windows 검증: `outputs/windows-phone-build/verification.json`, 빌드 로그: `outputs/windows-phone-build/logs/build.log`
- Mac ZIP: `outputs/mac-phone-build/we-are-table-mac.zip`
- 원본 편집기를 닫지 않고 사용한 별도 빌드 프로젝트: `work/mac-phone-build/project`. 다시 빌드할 때 원본 Assets·Packages·ProjectSettings를 먼저 동기화해야 합니다.
- 원본 Git의 미커밋 변경은 보존해야 합니다. 다른 컴퓨터에는 커밋·푸시하거나 변경 파일을 별도로 옮겨야 최신 소스가 전달됩니다.

## 2026-10-16 폰 네 대의 게임 화면 색·방향 표시 수정

폰이 맡은 다리는 튜토리얼 대상에서 제외돼 복도의 임시 색 숨김이 풀리지 않았습니다. 자이로 포즈도 기존 키 입력 화살표의 표시 조건에 포함되지 않았습니다.

- 호스트의 튜토리얼에 폰 소유 다리를 포함했습니다. 네 방향 터치 또는 자이로 기울기를 각각 연습하고 손을 떼거나 중립으로 돌아오면 다음 다리로 넘어갑니다. 숨겨진 다른 다리의 입력은 미리 완료 처리하지 않습니다.
- 자이로 기울기에 맞춰 PC 게임 화면의 다리 방향 화살표를 표시합니다. 기존 다리 물리와 색 선 굵기는 유지합니다. 작은 센서 잡음은 표시에서 제외합니다.
- 실제 Mac QA 게임을 로컬 NGO 호스트로 실행하고 공개 Cloudflare 중계를 통해 자동 폰 클라이언트 네 개를 연결했습니다. 로비 숨김, 네 다리별 튜토리얼·키 표시·화살표, 완료 후 전체 색, 색 옵션 on/off와 네트워크 씬 이동 후 off 유지 등 14개 검사가 통과했습니다.
- 실제 폰 센서와 Windows에서 직접 실행한 검사는 아닙니다. 검증 결과는 작업 자료 폴더의 `outputs/phone-leg-feedback/runtime/verification.json`, 실제 게임 화면은 `outputs/phone-leg-feedback/runtime/all-phone-arrows.jpg`입니다.
- 이 수정은 Unity 실행본에 들어갑니다. 폰 컨트롤러 고정 URL과 서버 코드는 그대로이며, 예전 게임 실행본에는 반영되지 않습니다.

### 위 수정이 포함된 최신 실행본

작업 자료 폴더의 다음 파일을 사용합니다. 앞서 만든 `mac-phone-build`·`windows-phone-build` ZIP에는 이 수정이 없습니다.

- `outputs/phone-leg-feedback/we-are-table-mac.zip`: Intel·Apple Silicon 공용, 빌드 오류 0개, 압축 CRC·압축 해제 후 전체 아키텍처 서명 검사 통과. 테스트용 임시 서명이며 Apple 공증은 하지 않았습니다.
- `outputs/phone-leg-feedback/we-are-table-windows.zip`: Windows AMD64, 빌드 오류 0개, ZIP CRC와 파일 205개 해시·Steam DLL 검사 통과. 실제 Windows 실행은 미검증입니다.
- 검사 전용 `PhoneFeedbackAudit`·`PhoneRuntimeAudit` 드라이버와 초기화 항목은 두 배포본에서 제외됐습니다.
