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
- 테이블(다리 보행): `Assets/Scripts/LegController.cs` (다리 피벗마다 부착, HingeJoint 모터 구동).
- 리셋(`1`키): `Assets/Scripts/SceneReset.cs` (상판에 부착, 초기 위치/회전 복원 + 다리 수직 보정).
- 그리드 바닥: `Assets/Materials/Ground.mat` + `Assets/Textures/Grid.png` (텍스처 타일링 방식).

## 시작 화면 / CLEAR
- ⚠️ **SampleScene.unity 파일은 수정하지 않는다** (사용자가 에디터에서 직접 맵 편집).
- **StartScreen.unity** (빌드 0번): 시작 화면. 환경 복제본 없이 카메라+라이트+StartMenu만 있고, `StartMenu`가 **SampleScene을 additive 로드**해 실제 맵을 배경으로 보여줌(맵 수정이 그대로 반영). 배경 게임플레이는 정지(스크립트 비활성 + Rigidbody kinematic), `FlyoverCamera`(height 11, 위에서 내려다봄)로만 비춤. IMGUI "PLAY" → SampleScene 단일 로드.
- **ClearChecker** (SampleScene의 빈 GO): 씬 내 모든 PressButton이 동시에 눌리면 IMGUI로 화면 상단에 "CLEAR" 표시.
- UI는 uGUI 대신 **IMGUI(OnGUI)** 로 구현(Canvas/EventSystem 불필요).
- 스크립트: `FlyoverCamera.cs`, `StartMenu.cs`, `ClearChecker.cs`.
- 빌드 순서: 0=StartScreen, 1=SampleScene.

## 테이블 프리팹 / 스테이지
- **Table 프리팹**: `Assets/Prefabs/Table.prefab` — `TablePlayer`(빈 부모) 아래 Table(상판)+다리4(피벗+메시). 다리는 런타임에 HingeJoint로 "Table"에 연결(이름으로 찾음). 카메라(CameraFollow)는 별도, 이름 "Table"을 자동 추적.
- **스테이지 구성**: StartScreen에서 선택. Stage1(*)·Stage2(**) = 새 씬(평지 그라운드 + 카메라 + 라이트 + 테이블). Stage3(***) = SampleScene(기존, 어려움).
- **StartScreen**: uGUI 3버튼(STAGE 1/2/3). `StartMenu`가 클릭 감지 → 해당 씬 로드. 배경은 SampleScene을 additive로 보여줌.
- 빌드 씬: StartScreen, SampleScene, Stage1, Stage2.
- ⚠️ Stage1/2의 테이블은 프리팹 인스턴스가 아니라 동일 구성의 복사본(프리팹은 별도 에셋). 필요시 프리팹 인스턴스로 교체 가능.

## ★ 세션 인계 가이드 (새 대화에서 먼저 읽기)
### 작업 규칙 (중요)
- 변경은 **에이전트가 파일을 직접 편집** → 사용자가 Unity에서 **저장 말고 reload(Don't Save)** 로 반영. (에디터에서 저장하면 파일 편집이 덮어써질 수 있음)
- **`Assets/Scenes/SampleScene.unity` 파일은 절대 수정 금지.** 사용자가 에디터에서 직접 맵 편집. (읽기는 가능)
- 씬/프리팹은 손으로 YAML 작성하거나 Python으로 생성. 편집 후 파이썬으로 무결성 검증(미해결 fileID 참조 0 확인).
- UI는 가능하면 **uGUI(Canvas) 또는 런타임 생성/3D**. EventSystem 회피 위해 클릭은 마우스 위치로 직접 감지(새 Input System).
- 입력 처리: **New Input System 전용**(activeInputHandler=1). 옛 `Input.GetAxis` 금지, `Keyboard.current/Mouse.current` 사용.

### 조작/게임 요약
- 다리 4개: WASD / TFGH / IJKL / 방향키. 상하=앞뒤 스윙(모터), 좌우=긴축(Y) 회전(+스윙축 동조). 자기 키 없으면 힌지 잠금.
- `1`키: 테이블 초기 위치 리셋(SceneReset).
- 홈 버튼 4개 다 누르면 CLEAR(3D 텍스트, 서서히 커짐, 유지).

### 주요 에셋 guid
- Table.mat 5e9a... / Button.mat c9d0e1f2... / HighFriction.physicMaterial e7f8a9b0...
- 스크립트: LegController c5d6e7f8.. / SceneReset a3b4c5d6.. / CameraFollow a3f1c2d4.. / FlyoverCamera e1f2a3b4.. / StartMenu f2a3b4c5.. / ClearChecker(파일명 동일) / PressButton b8c9d0e1..
- Table.prefab 0a1b2c3d.. / PressButton.prefab a7b8c9d0..
- 씬 guid: SampleScene 99c97208.. / StartScreen 11223344.. / Stage1 1111..aaaa / Stage2 2222..bbbb

### fileID 대역 관습(씬 내)
- 1500000xxx=Ground, 1600000xxx=Table/다리, 1700000xxx=플랫폼/램프, 1800000xxx=버튼, 1900000xxx=ClearChecker, 1950000xxx=StartMenu, 2000000xxx=Canvas/UI.
