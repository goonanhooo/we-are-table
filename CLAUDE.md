# 프로젝트 메모 (we are table)

## 재사용 컴포넌트: 누르는 버튼 (PressButton)
무게로 눌리는 물리 버튼. **다른 맵/위치에서도 재사용**한다.

- **프리팹**: `Assets/Prefabs/PressButton.prefab` (드래그해서 어디든 배치)
- **스크립트**: `Assets/Scripts/PressButton.cs`
- **머티리얼**: `Assets/Materials/Button.mat` (빨강)
- **동작**: 캡(작은 박스 Rigidbody)이 Y축으로만 이동, 약한 스프링으로 복귀.
  위에서 무게가 누르면 내려가고, `pressDepth` 이상 내려가면 `IsPressed=true` + `onPressed` 이벤트.
  치우면 올라오며 `onReleased`. 스프링이 약해 **작은 무게로도 눌림**.
- **주요 파라미터**: `pressDepth`(눌림 판정 깊이, 기본 0.06), `springForce`(복귀 세기, 작을수록 더 잘 눌림, 기본 10), `damping`(기본 2).
- **설치 팁**: 버튼이 무한히 내려가지 않도록 **아래에 바닥 스토퍼(콜라이더)** 가 있어야 함.
  현재 씬에선 플랫폼 홈 바닥이 스토퍼 역할. 캡 크기 0.13×0.04×0.13.
- **현재 사용처**: SampleScene의 플랫폼 홈 4곳 — `Button_LN/RN/LF/RF` (홈 위치 x=±0.58, z=3.42/4.58).

## 기타 핵심
- 테이블(다리 보행): `Assets/Scripts/LegController.cs` (다리 피벗마다 부착, HingeJoint 모터 구동). 입력 없을 때는 **현재 각도로 하드 고정**(힌지 한계를 현재 각 ±0.05°로 잠금, bounciness=0): 상판과의 상대 각도가 고정돼 다른 다리로 밀어도 **안 휨**(드리프트 없음). ⚠️ 단점: 하중 시 한계 벽에서 미세 떨림 가능 → Fixed Timestep 0.01 + solverIterations 40 + Rigidbody Interpolate로 억제. (브레이크 모터 방식은 안 떨리지만 finite force라 천천히 휨 → 하드 고정 채택)
- 리셋(`1`키): `Assets/Scripts/SceneReset.cs` (상판에 부착, 초기 위치/회전 복원 + 다리 수직 보정).
- 그리드 바닥: `Assets/Materials/Ground.mat` + `Assets/Textures/Grid.png` (텍스처 타일링 방식).
- ⚠️ **치트(빌드 시 제거 예정)**: `Assets/Scripts/PlaytestCheat.cs` (테이블 루트에 부착, guid b0c1d2e3..). **Shift + 방향키 = 테이블 수평 슬라이드**(본체+다리만 함께 이동). 프리팹 루트(9100000040) + 각 씬 Table 루트(1600009040). 빌드 전 이 컴포넌트/스크립트 제거.
- 물리 폭발 복구: `Assets/Scripts/TableSafety.cs` (테이블 루트에 부착, guid a9b8c7d6..). 씬의 모든 Rigidbody를 감시해 위치/속도가 Inf/NaN이면 **마지막 정상 상태로 복구 + 속도 0** → "Skipped updating the transform ... infinite" 에러로 안 깨지고 계속 진행. 프리팹 루트(9100000030) + 각 씬 Table 루트(1600009030)에 부착(StartScreen/Hallway는 프리팹에서 자동).

## 시작 화면 / CLEAR
- **StartScreen.unity** (빌드 0번): 시작 화면. **고정 카메라**(pos (0,1.9,-2.8), pitch 24, FlyoverCamera/CameraFollow 없음, SMAA on) + **흰 배경**(ClearFlags=SolidColor white). 테이블은 **실제 Table 프리팹 인스턴스**(fileID 1650000000, 위치 (1.1,0,0.3) 오른쪽 → 편집 모드에서도 보임). 프리팹 인스턴스 수정으로 **PauseMenu(9100000010)·LegColorXray(9100000020)만 비활성**, 나머지(LegController/물리)는 그대로 → **키보드로 게임과 동일하게 조작**. `StartMenu`는 **단단한 흰 박스 바닥(윗면 y=0)만 런타임 생성** + 왼쪽 **uGUI PLAY 버튼**(마우스 위치 클릭) → SampleScene 로드. ⚠️ 다른 씬과 달리 **StartScreen만 Table 프리팹 인스턴스를 사용**(프리팹 물리 = Stage1 복사본과 동일함을 확인).
- **ClearChecker** (SampleScene의 빈 GO): 씬 내 모든 PressButton이 동시에 눌리면 IMGUI로 화면 상단에 "CLEAR" 표시.
- 스크립트: `StartMenu.cs`, `ClearChecker.cs`. (`FlyoverCamera.cs`는 현재 미사용)
- 빌드 순서: 0=StartScreen, 1=SampleScene.

## 테이블 프리팹 / 스테이지
- **Table 프리팹**: `Assets/Prefabs/Table.prefab` — `TablePlayer`(빈 부모) 아래 Table(상판)+다리4(피벗+메시). 다리는 런타임에 HingeJoint로 "Table"에 연결(이름으로 찾음). 카메라(CameraFollow)는 별도, 이름 "Table"을 자동 추적.
- **스테이지 구성**: StartScreen에서 선택. Stage1(*)·Stage2(**) = 새 씬(평지 그라운드 + 카메라 + 라이트 + 테이블). Stage3(***) = SampleScene(기존, 어려움).
- **StartScreen**: 고정 카메라+흰 배경, 오른쪽에 Table 프리팹(런타임 생성·정지), 왼쪽에 PLAY 버튼 1개 → SampleScene 로드. (이전의 STAGE 1/2/3 버튼/additive 배경은 제거됨)
- 빌드 씬: StartScreen, SampleScene, Stage1, Stage2, Hallway.

## Hallway 씬 (컷신으로 시작하는 플레이 스테이지)
- **Hallway.unity** (빌드 4번): 닫힌 **흰 복도** + 끝에 **의자**(테이블과 같은 Table.mat 색, 큐브로 구성). 환경(복도/벽/천장/트랩도어/의자/구름)은 **런타임에 `HallwayStage.cs`가 생성**(씬 YAML엔 카메라·라이트·볼륨·Table 프리팹 인스턴스만).
- **흐름**: 입장 → 카메라가 테이블을 정면에서 `lookSeconds`(5초) 응시 → 드라마틱하게 의자 쪽으로 팬(발견) → 컷신 끝나면 테이블 조작 가능(컷신 동안엔 Rigidbody kinematic으로 정지=입력 무시). 의자 앞 **정사각형 트랩 구역(z 5~11, 6×6)** 의 **중앙(z=8)** 에 들어서면, **두 짝짜리 바닥문이 복도 양 가장자리 경첩 기준 가운데서 갈라지며 아래로** 열려 테이블이 **낙하**(물리 그대로, 약한 drag 0.4로 5초간 — 다리는 브레이크 모터라 떨림 없음) → `fallToNextDelay`(5초) 후 낙하 속도를 `FallCarry`(static)에 담아 **Jungle 씬으로 전환**. 낙하 중 카메라는 테이블을 바짝 추적. (구름 제거됨)

## Jungle 씬 (정글 — 물섬 + 거대 구멍/용암/블랙홀)
- **Jungle.unity** (빌드 5번) + `JungleStage.cs`(guid c7d8e9f0.., 씬 guid 44556677..). 환경 전부 런타임 생성.
- **물로 둘러싸인 섬**: 가운데 정사각 구멍이 뚫린 프레임형 섬(잔디색) + 사방 물(큰 평면). 누워있는 **더미 기린**(노란 박스들)이 착지점에서 구멍 건너 조금 멀리 보임.
- **공중 낙하 이어받기**: Hallway에서 떨어지던 속도(`FallCarry.ySpeed`)로 섬 위 공중(`spawnPoint`)에서 같은 속도로 이어 떨어져 착지(자연 물리, 살짝 튕김).
- **거대 구멍**: 중앙 14×14, 깊이 70. 바닥에 **용암**(emissive). **상판(TableTop)이 용암 높이에 닿으면 사망**(현재 씬 재시작). 구멍 중앙엔 **빛나는 블랙홀**(emissive 구체) — `pullRadius` 내 진입 시 끌려가고 `captureRadius`면 흡입(재시작). ⚠️ 사망/흡입은 현재 씬 재시작 placeholder.
- **카메라**: Stage1과 동일하게 **CameraFollow**(마우스 시점 회전, distance 6/height 1.5, lockCursor). JungleStage는 카메라를 제어하지 않음.
- 빌드 씬 순서: StartScreen, SampleScene, Stage1, Stage2, Hallway, Jungle.
- 카메라: 컷신은 `HallwayStage`가 직접 제어, 컷신 후 LateUpdate로 테이블 뒤를 추적. 배경=스카이박스(기본 procedural), 실내는 flat ambient로 밝게.
- 스크립트 guid: HallwayStage f6a7b8c9.. / 씬 guid: Hallway 33445566..
- ⚠️ 낙하 후 "하늘 배경 스테이지"는 아직 별도로 없음(현재 fall 중에만 하늘이 보이고 다음 씬은 기존 Stage). 필요시 하늘 테마 착지 씬 추가 가능.
- ⚠️ **모든 씬(SampleScene/Stage1/Stage2)의 테이블은 프리팹 인스턴스가 아니라 동일 구성의 복사본**(루트 GO 이름은 "Table", fileID `1600000000`). 프리팹(`Table.prefab`)에 컴포넌트를 추가해도 씬에는 반영 안 됨 → **프리팹과 각 씬 Table 루트 양쪽에 똑같이 추가**해야 함.

## 일시정지 메뉴 / 다리 색(X-ray)
- **PauseMenu** (`Assets/Scripts/PauseMenu.cs`): `ESC` 토글로 일시정지(`Time.timeScale=0`). IMGUI로 그리되 **클릭/슬라이더는 `Mouse.current`로 직접 처리**(New Input System 전용이라 `GUI.Button` 클릭이 안 먹음). 메뉴(영어): Resume / Volume 슬라이더(`AudioListener.volume`) / Color ON·OFF / Main Menu(StartScreen). 시작 화면 additive 배경에선 비활성(`gameObject.scene != activeScene` 가드).
- ⚠️ `ESC`는 PauseMenu 전용. **CameraFollow의 ESC 커서토글은 제거**(충돌 방지). 일시정지=커서 표시, 복귀=커서 Locked(마우스 시점 복원).
- **LegColorXray** (`Assets/Scripts/LegColorXray.cs`): 다리 4개(Leg_FR/FL/BR/BL)를 이름으로 찾아 박스 12모서리를 그림. **모서리마다 독립된 2점 LineRenderer**(다리당 12개) — 한 붓 그리기 경로의 "꼬임" 방지. 색 FR=빨강·FL=초록·BR=파랑·BL=노랑(기본 off, 메뉴에서 토글). 선명한 단색 선이며, X-ray는 `Assets/Shaders/XrayLine.shader`(ZTest Always, queue Overlay)로 몸통을 통과해 항상 화면 위에 보임. 다리는 씬마다 별도 루트라 **이름 전역 검색**(`GameObject.Find`)으로 찾음.
- ⚠️ 두 컴포넌트는 `Table.prefab`의 TablePlayer 루트(fileID 9100000010/9100000020)와 **SampleScene/Stage1/Stage2의 Table 루트(fileID 1600009010/1600009020)** 양쪽에 부착됨.

## ★ 세션 인계 가이드 (새 대화에서 먼저 읽기)
### 작업 규칙 (중요)
- 변경은 **에이전트가 파일을 직접 편집** → 사용자가 Unity에서 **저장 말고 reload(Don't Save)** 로 반영. (에디터에서 저장하면 파일 편집이 덮어써질 수 있음)
- 씬/프리팹은 손으로 YAML 작성하거나 Python으로 생성. 편집 후 파이썬으로 무결성 검증(미해결 fileID 참조 0 확인).
- UI는 가능하면 **uGUI(Canvas) 또는 런타임 생성/3D**. EventSystem 회피 위해 클릭은 마우스 위치로 직접 감지(새 Input System).
- 입력 처리: **New Input System 전용**(activeInputHandler=1). 옛 `Input.GetAxis` 금지, `Keyboard.current/Mouse.current` 사용.

### 조작/게임 요약
- 다리 4개: WASD / TFGH / IJKL / 방향키. 상하=앞뒤 스윙(모터), 좌우=긴축(Y) 회전(+스윙축 동조). 자기 키 없으면 힌지 잠금.
- `1`키: 테이블 초기 위치 리셋(SceneReset).
- 카메라(`CameraFollow`): 마우스로 시점 회전 + **휠로 줌(distance 2.5~14)**. SampleScene/Stage1/Stage2/Jungle 공통. (StartScreen=메뉴 고정, Hallway=컷신 → CameraFollow 없음)
- 홈 버튼 4개 다 누르면 CLEAR(3D 텍스트, 서서히 커짐, 유지).

### 주요 에셋 guid
- Table.mat 5e9a... / Button.mat c9d0e1f2... / HighFriction.physicMaterial e7f8a9b0...
- 스크립트: LegController c5d6e7f8.. / SceneReset a3b4c5d6.. / CameraFollow a3f1c2d4.. / FlyoverCamera e1f2a3b4.. / StartMenu f2a3b4c5.. / ClearChecker(파일명 동일) / PressButton b8c9d0e1.. / PauseMenu c2d3e4f5.. / LegColorXray b1c2d3e4.. / XrayLine.shader d3e4f5a6..
- Table.prefab 0a1b2c3d.. / PressButton.prefab a7b8c9d0..
- 씬 guid: SampleScene 99c97208.. / StartScreen 11223344.. / Stage1 1111..aaaa / Stage2 2222..bbbb

### fileID 대역 관습(씬 내)
- 1500000xxx=Ground, 1600000xxx=Table/다리, 1600009xxx=Table 루트의 PauseMenu(9010)/LegColorXray(9020), 1700000xxx=플랫폼/램프, 1800000xxx=버튼, 1900000xxx=ClearChecker, 1950000xxx=StartMenu, 2000000xxx=Canvas/UI.
