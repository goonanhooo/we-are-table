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
- **StartScreen.unity** (빌드 0번): 시작 화면. **고정 카메라**(pos (0,1.9,-2.8), pitch 24, FlyoverCamera/CameraFollow 없음, SMAA on) + **흰 배경**(ClearFlags=SolidColor white). 테이블은 **실제 Table 프리팹 인스턴스**(fileID 1650000000, 위치 (1.1,0,0.3) 오른쪽 → 편집 모드에서도 보임). **PauseMenu(9100000010)·LegColorXray(9100000020)는 활성**(옵션 표지판/ESC로 옵션 메뉴를 열기 위해), 나머지(LegController/물리)도 그대로 → **키보드로 게임과 동일하게 조작**. `StartMenu`의 흰 바닥(`StartGround`, 윗면 y=0)은 이제 **씬에 직접 구워진 편집 가능 오브젝트**(과거엔 런타임 생성) — `StartMenu.BuildGround`는 씬에 없을 때만 만드는 폴백. 메뉴는 **3D 나무 표지판 클릭**(uGUI 버튼 없음): Camera.main 레이캐스트로 `Sign_Play` 클릭→**Hallway 로드**, `Sign_Option` 클릭→**PauseMenu.OpenMenu()**(ESC 옵션과 동일). 커서는 매 프레임 강제로 보이게(Unlock). ⚠️ 다른 씬과 달리 **StartScreen만 Table 프리팹 인스턴스를 사용**(프리팹 물리 = Stage1 복사본과 동일함을 확인).
- ★ **시작화면 타이틀**(`Canvas`(ScreenSpaceOverlay, CanvasScaler 1920×1080 match0.5) > `Title` > `we`/`are`/`table`): 손글씨 `제목_없는_아트워크 36.png`의 타이틀 줄을 단어별로 잘라 `Assets/Signs/Title_we.png`·`Title_are.png`·`Title_table.png`(투명 배경 검정 글씨). **좌상단**에 원본 레이아웃 비율대로 배치(표지판 글씨보다 크게, scale≈1.7). 각 단어에 `TitleWobble`(amplitude/speed/phase 제각각, unscaledTime sin)로 **위아래로 조금씩 귀엽게** 흔들림.
- ★ **시작화면 나무 표지판 2개**(`Signs` GO > `Sign_Play`/`Sign_Option`): 화면 **왼쪽 아래 바닥**에 가로로 2개(클릭 메뉴). **기둥 있는 전체 표지판**(`WoodenSignPost.fbx`의 메시 그대로) + 프레임 구멍에 **흰 종이 Quad**(손글씨 Play/Option). 구조: scale1 빈 루트(yaw180) + **동적 Rigidbody**(mass1, 중력O, 무게중심 낮게(0,0.22f,0), ContinuousDynamic) + BoxCollider(center(0,0.5f,0) size(0.86f,1.0f,0.14f), 바닥 y=0에 닿게) → **평소 수직으로 서있고 테이블이 부딪히면 멈추거나 쓰러짐**. 자식: `Frame`(FBX 메시, localScale 100·f) + `Paper`(Quad, 콜라이더 제거, localPos(0,0.662f,0.030f), scale(0.72f,0.46f)). f=0.62. 종이 텍스처는 `SignPlay.png`·`SignOption.png`(URP/Unlit). 위치: Sign_Play(-1.32,0,-0.76), Sign_Option(-0.65,0,-0.71). 클릭→레이캐스트가 루트 이름으로 판별(`StartMenu`). (기둥 없는 가로형 `SignFrameOnly.asset`도 있으나 미사용 — 사용자가 기둥+물리 버전 선호.)
  - ⚠️ **FBX 100배 함정**: 이 FBX는 메시가 0.01단위라 임포트 루트 localScale=**100**(useFileScale·fileScale0.01). 루트 스케일을 직접 덮어쓰면(예: 0.62) 0.006배로 사라짐 → **scale1 빈 루트 아래에 FBX(스케일 100×0.62=62)와 종이(월드단위)를 각각 자식**으로 두는 구조로 해결.
  - ⚠️ **프레임 구멍 정밀 검출**: FBX를 마젠타 배경으로 오쏘 정면 렌더 → 가장자리 flood-fill로 '바깥' 제외 → 남은 마젠타=구멍. 로컬 center(0,0.662) size(0.72,0.46).
- **ClearChecker** (SampleScene의 빈 GO): 씬 내 모든 PressButton이 동시에 눌리면 IMGUI로 화면 상단에 "CLEAR" 표시.
- 스크립트: `StartMenu.cs`, `ClearChecker.cs`. (`FlyoverCamera.cs`는 현재 미사용)
- 빌드 순서: 0=StartScreen, 1=SampleScene.

## 테이블 프리팹 / 스테이지
- **Table 프리팹**: `Assets/Prefabs/Table.prefab` — `TablePlayer`(빈 부모) 아래 Table(상판)+다리4(피벗+메시). 다리는 런타임에 HingeJoint로 "Table"에 연결(이름으로 찾음). 카메라(CameraFollow)는 별도, 이름 "Table"을 자동 추적.
- **스테이지 구성**: StartScreen에서 선택. Stage1(*)·Stage2(**) = 새 씬(평지 그라운드 + 카메라 + 라이트 + 테이블). Stage3(***) = SampleScene(기존, 어려움).
- **StartScreen**: 고정 카메라+흰 배경, 오른쪽에 Table 프리팹, 왼쪽 아래에 나무 표지판 2개(Play→Hallway, Option→옵션 메뉴). uGUI 버튼/STAGE 버튼/additive 배경 없음.
- 빌드 씬: StartScreen, SampleScene, Stage1, Stage2, Hallway.

## Hallway 씬 (컷신으로 시작하는 플레이 스테이지)
- **Hallway.unity** (빌드 4번): 닫힌 **흰 복도** + 끝에 **의자**(Table.mat 색).
- ✅ **환경 = 편집 가능한 실제 씬 오브젝트로 전환됨**(과거엔 `HallwayStage`가 런타임 생성). `Hallway` 부모 GO 아래 Floor_A/B·TrapL/R·Wall_L/R/Back/Front·Ceiling·Chair. **콜라이더+물리 머티리얼은 Stage1 바닥과 동일**(BoxCollider + **HighFriction**, 색만 `HallwayWhite.mat` 흰색). **벽 두께 0.4→2.0**(안쪽 면 유지=복도 폭 불변). 에디터에서 직접 이동/편집 가능.
- `HallwayStage.Start`는 이제 환경을 **생성하지 않고** 트랩 문짝만 `GameObject.Find("TrapL"/"TrapR")`로 찾아 컷신 낙하 연출에 사용. ⚠️ 스크립트의 `BuildHallway/BuildChair/Box/AddPart/MakeMat`·`whiteMat`은 **레거시(미호출)** — 지오메트리 참고용. 환경 수정은 씬에서 직접(코드 아님).
- ★ **상상 말풍선 이미지**(`ThoughtImage` 월드 캔버스 + `ThoughtReveal.cs`): 컷씬 초반 테이블 응시(정지) 동안 사용자 손그림 `제목_없는_아트워크 38.png`(의자❤️ 말풍선, 1920×1080 투명배경)을 화면에 띄움. 이미지를 4레이어로 분리(`Assets/Thought/Thought_{1_tip,2_mid,3_near,4_body}.png` = 꼬리끝 puff→중간→큰 puff→본체). **카메라 A 시야를 꽉 채우는 월드 캔버스**(D=1.6, FOV60·16:9 프러스텀에 맞춰 위치/스케일, LookRotation(forward,up)로 미러링 없음)라 이미지 위치 그대로(빈공간 포함). 4레이어 각 CanvasGroup. **등장: 0.6초 간격으로 꼬리끝→…→본체 순 스르륵 페이드인**(SmoothStep 0.45s). 계속 떠 있다 **카메라가 움직이기 시작하면**(초기 포즈 대비 이동/회전 임계 초과) 월드 고정이라 **그 자리에서 페이드아웃(1.2s)+프레임아웃**, 끝나면 비활성. ⚠️ **런타임 생성 아님 — 씬에 저장된 편집 가능한 오브젝트**(ThoughtImage transform 이동/회전/스케일로 위치 조정). 편집 모드에선 alpha1로 보이고(ExecuteAlways 아님) 플레이 시 OnEnable이 0으로 리셋 후 연출.
- **흐름**: 입장 → 카메라가 테이블을 정면에서 `lookSeconds`(5초) 응시 → 드라마틱하게 의자 쪽으로 팬(발견) → 컷신 끝나면 테이블 조작 가능(컷신 동안엔 Rigidbody kinematic으로 정지=입력 무시). 의자 앞 **정사각형 트랩 구역(z 5~11, 6×6)** 의 **중앙(z=8)** 에 들어서면, **두 짝짜리 바닥문이 복도 양 가장자리 경첩 기준 가운데서 갈라지며 아래로** 열려 테이블이 **낙하**(물리 그대로, 약한 drag 0.4로 5초간 — 다리는 브레이크 모터라 떨림 없음) → `fallToNextDelay`(5초) 후 낙하 속도를 `FallCarry`(static)에 담아 **Jungle 씬으로 전환**. 낙하 중 카메라는 테이블을 바짝 추적. (구름 제거됨)

## Jungle 씬 (오픈월드 섬 — 절차적 지형 + 화산 + 용암의 강)  ★전면 재설계
- **Jungle.unity** (빌드 5번) + `JungleStage.cs`(guid c7d8e9f0.., 씬 guid 44556677..). 지형/바다/용암은 **씬의 실제 오브젝트 + 절차적 메시 컴포넌트**(ExecuteAlways라 에디터에서도 보임·편집). `JungleStage`는 **낙하 이어받기 + 용암 사망/리스폰만** 담당. 씬 생성기: `/tmp/mk_header.py`+`/tmp/gen_scene2.py`(세션 한정).
- **섬(절차적)**: `Island` = `IslandTerrain.cs`(guid ..9005, ExecuteAlways) — **원형 falloff + Perlin fBm 해안선**으로 둥글지만 자연스러운 섬 메시 + **화산 원뿔/분화구 융기** + **용암 강 도랑 파기**를 한 메시로 생성, 높이/경사별 **모래·잔디·바위 정점색**, MeshCollider 자동. `Custom/VertexColorLit` 셰이더(guid ..9007). 편집: `islandRadius/shoreWidth/landHeight(=12)/hill*/volcano*/riverPath/riverWidth/seed` 인스펙터 값(중심 islandCenter(0,127), R=150, 화산 (36,188) 높이 64). **스폰 착지 언덕**: `spawnHillCenter(0,0)/spawnHillRadius(24)/spawnHillHeight(5.5)` — 초기 착지/리스폰 지점을 완만한 둔덕으로(HeightAt(0,0) ~16). landMask 곱이라 육지에만.
  - ★ **용암 협곡(canyon, 좁고 가파름)**: 강이 평지에 붙어 흐르던 걸 **깊고 좁은 가파른 골짜기**로(긴 다리 테이블이 건널 수 있게). `landHeight` 6→**12**(고지대 림), 강 중심으로 국소지형−`canyonDepth(실제 27.8)` 까지 바닥을 내림(물 위 유지).
  - ★ **화산 분화구→경사면 용암 흐름**: 예전엔 깊은 협곡(27.8)이 화산 콘 정상을 그대로 관통해 **콘을 둘로 가르는 균열**이 생겨 어색했음. 수정: 협곡 깊이를 콘 위에서만 얕게 — `localCanyonDepth = lerp(canyonDepth, volcanoChannelDepth(5), cone)` (`cone=clamp01(1−vd/volcanoRadius)`). **vd≥volcanoRadius(70)에선 cone=0 → canyonDepth 그대로라 강/협곡 형태(높이·폭) 완전 불변**(검증). 결과: 정상 분화구(중심 H 19→**42**, 림~53)에 용암이 고였다가 경사면을 타고 채널로 흘러내려 협곡으로 자연 합류. 폭(floorHalf/rimHalf)은 안 바꿈.
    - **벽폭 = `canyonRimHalf` − `canyonFloorHalf`** 가 핵심(작을수록 거의 수직). 노이즈(`canyonRimNoise`)는 floor/rim 을 **같이 밀어 폭은 유지한 채 굽이치게**(메안더) — rimHalf 줄이면 실제로 좁아짐. **현재 값은 사용자가 인스펙터에서 직접 조정**(좁고 깊은 슬롯: floorHalf≈1.9·rimHalf≈2.1·noise 0·depth 큼 → 바닥폭 ~3.8·거의 수직 벽). **협곡 크기는 사용자 튜닝 존중**(코드에서 임의 변경 금지).
    - ⚠️ **메시 해상도 필수**: 격자 간격(size/res)보다 좁은 벽은 뭉개져 완만해 보임. 그래서 `resolution`=**340**(Range 상한 480, 격자 ~2유닛)로 올려 가파른 벽 표현. (verts ~116k, UInt32 자동.)
    - ⚠️ **바다 구간엔 협곡 안 팜**(`landAmt = SStep(water+0.5, water+4, h)` 곱) → 강이 바다로 가며 carve 가 사라져 **인공 육교/바다로 뻗는 용암 없음**.
    - `CanyonRimHalf`(=rimHalf+noise) 프로퍼티 = 리스폰 안전거리 기준. 기린/철창은 강에서 14~16 거리라 협곡 **밖 고지대** 유지.
- **용암의 강**: `LavaRiver` = `LavaRiver.cs`(guid ..9006)가 IslandTerrain의 **강 경로/바닥 높이를 그대로 따라** 용암 리본 메시 + 분화구 웅덩이 생성(`Custom/Lava` 셰이더 ..9002, 흐르는 이미시브 HDR). 화산에서 흘러나와 길을 가로지름(중심선이 x=0를 z≈46에서 통과). 콜라이더 없음(시각). **편집: IslandTerrain.riverPath(공유 소스)로 경로 변경 → 지형 도랑·용암·사망판정 동시 반영.**
- **사망/리스폰**: 테이블 콜라이더가 **실제 용암 리본 표면에 닿으면**(리본 반폭 안 + 바닥이 용암표면 이하, 표면=강 중심선 HeightAt+lift+리본 transform.y) 사망 → **빠진 자리에서 가장 가까운 안전한 둑**으로 리스폰(아래 "리스폰/리셋" · "용암 사망" 항목). (씬 재시작 아님)
- **거리 규칙**(Hallway 책상↔의자=23유닛 기준): **바다↔테이블 ≈ 23**(해안선 z≈-18, 테이블 스폰 z0), **스폰↔용암강 ≈ 46(=2×23)**(강 z46).
- ★ **야자수(정글 식생)**: `Palms` GO 아래 ~200그루. 소스 FBX 3종 `Assets/Palms/{3D Palm Tree2, Stylized Palm Tree, Coconut Palm Tree}.fbx`(⚠️ **0.01단위 메시 → scale 100=1유닛**, 그래서 인스턴스 localScale=100·H로 키 H 7~13). 절차적 배치(seed 20240621): 육지(h 3~36)·완경사·용암협곡 밖(rd>13)·기린/스폰/건물 풋프린트 제외. 각 그루 = scale1 루트(지형높이 위치 + **CapsuleCollider**=충돌, **static**) > FBX 비주얼(랜덤 yaw/미세틸트). **텍스처 없는 회색 FBX라 정점색을 구워 칠함**: 메시 복사본(`Assets/Palms/vc_*.asset`)에 **로컬 Y 정규화로 줄기=갈색→잎=초록** 램프(임계 0.66~0.78), `Custom/VertexColorLit` 공유 머티리얼. 재배치/재색칠은 코드(에디터 RunCommand)로.
- ★ **거대 창고 건물**(`Warehouse` GO): 기린 반대편(용암강 건너 **side −1**, 평탄지 중심 (-78,82) 높이~12.4)에 **엄청 큰 흰 직사각형**(54W×76L×30H, 흰 URP/Lit). 큐브 6면(바닥/지붕/뒤·좌·우 벽 + 정면 좌/우/인방) 각 **BoxCollider 유지(충돌)**. **정문(20×20 개구부)은 기린 쪽을 향함**(yaw=atan2(dir.x,dir.z)≈123°, local +Z 정면). **내부 Point Light 5개**(천장 근처, 따뜻한 흰색)로 안이 환함 + 정문으로 햇빛 유입. 위치/크기는 코드로 빌드.
- **낙하 자세 이어받기**: `FallCarry`가 자세 전체 전달(`hasPose`+`rotation`+`velocity`+`angularVelocity`). ⚠️ **상판(Table) Rigidbody의 실제 월드 회전을 캡처**(빈 부모 root는 안 구르고 identity라 무의미 — 과거 버그). JungleStage.Start는 spawnPoint에서 **상판을 carried 회전 R로 두고 전체를 강체회전**(부모 Transform만 옮기면 동적 자식 안 따라옴)해 모든 Rigidbody에 R/각속도/선속도 직접 적용 → Hallway 추락 자세 그대로 이어짐. (검증: Euler(35,0,25)/angVel(2,0,1)/vy-12 주입→그대로 적용 확인.)
- **바다**: `Ocean` = `OceanWater.cs`(..9003)+`Custom/OceanWater`(..9001, Gerstner 파도, **UV·메시노멀 안 씀·positionWS만·Cull Off** → 토폴로지 자유). 콜라이더 없음(섬 콜라이더가 바닥).
  - ★ **방사형 LOD 디스크로 최적화**: 예전 균일 그리드(3000²·**9만 정점**, 먼 물도 같은 밀도라 낭비)를 **중심 촘촘·바깥 기하급수 듬성**한 디스크로 교체 → **~1.6만 정점(1/6)**·드로우콜 1. `maxRadius`(8000)를 크게 잡아도 먼 링이 듬성해 가벼움 → **화산 1.5배(~120) 고공에서 사방 수평선까지 덮음**(검증). `followCamera`(플레이 중 카메라 XZ 추적, y=물높이 유지 — 파도는 월드 XZ라 안 밀림). 튜닝: `maxRadius/ringGrowth(1.07≈근거리10유닛)/angularSegments(144)`. `receiveShadows=off`(평면이 섀도캐스케이드 들어갈 필요 없음).
- **기린(FBX 모델)**: `Assets/Models/Giraffe.fbx`(임포터 useFileScale=false/globalScale=1 → ~1유닛, 인스턴스 scale 3 = ~3유닛). 씬에 `Giraffe`(프리팹 인스턴스)로 (6, **지면 ~11.3**, 28) — 협곡 림 고지대(협곡 도입으로 landHeight 올라가 y 5.27→11.3 재배치). 철창/버튼/RespawnPoint도 이 지면에 맞춰 재배치. (옛 큐브 더미 대체.) **나무 미배치**(사용자 추가 예정).
  - ⚠️ **텍스처(임베드 추출)**: FBX에 diffuse/normal PNG가 임베드돼 있었으나 Unity가 자동 추출/연결 안 함 → 회색. FBX 바이너리에서 **PNG 시그니처로 직접 추출**(`python3`로 `\x89PNG`~`IEND` 잘라냄)해 `Assets/Models/Giraffe_diffuse.png`(무늬)·`Giraffe_normal.png`(노멀맵 타입) 저장 → `GiraffeMat.mat`(URP Lit, _BaseMap+_BumpMap) 만들어 렌더러에 적용. (다른 임베드텍스처 FBX도 동일 방식.)
- **기린 철창 + 버튼 → 컷신 → 지라프 모드 개방**:
  - **철창**(`GiraffeCage` GO, 씬에 실린더 바 34개): 세로바 16(5×5 경계)+지붕격자 10+중간링 4+**바닥프레임 4(네 변, 지면 닿게 y≈0.06)**, `Assets/Models/SilverMat.mat`(**은색**: BaseColor 밝은회색·Metallic 0.45·Smoothness 0.62 — 메탈릭 1.0이면 환경반사로 어둡게 보여 낮춤). 기린 둘레 hx/hz 1.7·높이 4로 감쌈. (RunCommand 골든템플릿으로 생성·저장. 옛 IronMat 대체.)
  - **버튼**(`ButtonAssembly` > `ButtonBase` 낮은 받침 + `CageButton` **납작한** 빨간 캡 scale.y 0.035): 철창 **앞(기린→spawn(0,0) 방향)** ~(5.3,5.34,24.7). `CageButton.cs` = 테이블 강체가 **pressRadius(1.3) 안에 들면**(아주 민감) 1회 눌림 → 캡 내려감(capDip 0.05) → **철창이 riseHeight(60)만큼 riseSeconds(3.2) 동안 천천히 상승(ease)→시야 밖→`SetActive(false)`(사라짐)** → 0.3s 후 `cutscene.Play()`.
  - **컷신**(`GiraffeCutscene.cs` guid ..9009, GO "GiraffeCutscene"): **근접 자동발동 끔(`autoTrigger=false`)** — 버튼 `Play()`로만 발동. ① 기린 기립(Start에서 lyingTilt 88°로 눕힘→테이블 향함) ② 대사 "날 도와줘서 고마워.." → "내 힘을 줄게..." ③ 옆 앵글 전환 ④ 품속 발광 구체→테이블 흡수 ⑤ 테이블 emission 번쩍 ⑥ 시스템 메시지 "기린의 힘을 얻었다 / 2번 버튼을…" + **`GiraffeMode.locked=false`(개방)**. 컷신 중 테이블 kinematic 정지 + CameraFollow 끄고 직접 제어.
  - **`GiraffeMode.locked`**: true면 2번 토글 무시. Jungle은 GiraffeCutscene.Start에서 locked=true(개방 전), 컷신 끝에 false. (다른 씬 기본 false.)
  - (검증: 버튼 근처로 테이블 텔레포트→눌림→철창 y 5.27→65 상승·비활성→컷신 실행→기린 직립·locked=false 확인.)
  - ⚠️ **IMGUI 한글**: 기본 폰트가 한글 못 그려서 `Font.CreateDynamicFontFromOSFont("Apple SD Gothic Neo" 등)` 로드해 OnGUI 대사/메시지 렌더(검증됨). ⚠️ 백그라운드 Play는 명령 사이에 몰아 진행돼 특정 컷 캡처가 어려움(timeScale로 멈춰서 확인).
- **카메라**: **CameraFollow**(마우스 시점, distance 7, lockCursor). 빌드 순서: StartScreen, SampleScene, Stage1, Stage2, Hallway, Jungle.
- ⚠️ **`IsNormalized(dir)` assertion 해결**: 손으로 박던 라이트/카메라 쿼터니언이 비정규(노름≈0.9987)라 URP가 라이트 방향에서 assert → **오일러(Y*X*Z)에서 정확히 계산해 노름 1.0**으로 수정.
- ⚠️ **섬 높이 폭발 버그 해결(중요)**: `IslandTerrain`에서 GLSL식 `smoothstep(edge0,edge1,x)` 의도로 **Unity `Mathf.SmoothStep(from,to,t)`를 오용**(이건 from~to 보간이라 0~1이 아님) → 높이가 127~ 수천까지 폭발(메시 Y 8000+). **자체 `SStep(e0,e1,x)` 헬퍼**로 전부 교체해 해결. C# 에서 GLSL smoothstep 쓸 땐 `Mathf.SmoothStep` 금지.
- ✅ **MCP로 Play 검증 완료**: 테이블 낙하→섬 착지, 화산 분화구 용암+사면 흐름, 용암강 발광, 기린 배치, 런타임 에러 0. 튜닝값: riverDepth 4.5 / riverWidth 15 · LavaRiver width 13 / lift 0.35 · Lava.mat _CrustWidth 0.32 / _Emission 3.2.
- **용암(협곡 바닥 흐름 + 바다 경계 옵시디언 + 발광)**:
  - LavaRiver는 `island.HeightAt(강중심)+lift(1)` 를 따라 협곡 바닥에 얹힘. `width`는 **바닥폭(2·floorHalf)에 맞춰 자동 의도**(현재 ~3.46) — 안 맞추면 좁은 협곡 밖으로 삐져나옴.
  - **바다로 안 뻗게 truncate**: 바닥이 물높이에 닿는 경로비율 `SeaEndU` 까지만 리본을 그림(`uMax`) → 용암이 해안에서 끝남.
  - **용암 어귀 돌(옵시디언)** `ShoreRocks.cs`(ExecuteAlways, `ShoreRocks` GO, `ObsidianMat`): **용암-바다가 맞닿는 어귀에만** 각진 검은 바위 덩어리들을 무리지어 생성. `FindLavaMouth()`(강 경로에서 지형이 물높이 닿는 지점)를 찾아, 그 해안을 따라 `rockCount`(16)개의 **low-poly 페이셋 바위**를 흩뿌림 — 각 바위는 정이십면체를 Perlin 노이즈로 찌그러뜨리고 **플랫셰이딩**(각 면 별도 노멀)해 매끈한 구가 아닌 **각진 돌**로. (섬 둘레 전체 X·능선 X — 어귀에 바위 무리. 능선 방식은 꼭짓점이 뾰족해 '바늘'처럼 보여서 폐기.) 튜닝: `rockCount/spreadLength/rockMin·Max/lumpiness`.
  - **발광/그늘 너무 검정 해결**: Lava 셰이더는 언릿 이미시브라 **주변 벽을 못 밝힘**. 그늘이 검게 떨어지는 표준 해법 조합을 적용:
    1. **앰비언트(`RenderSettings` Trilight)** ↑ — 직접광 안 닿는 면의 전역 채움(1순위).
    2. **메인 디렉셔널 `shadowStrength` 0.65** — 그림자 자체를 덜 검게(앰비언트가 비침).
    3. **`FillLight`**(보조 디렉셔널, **그림자 OFF**, intensity 0.32, 메인광 yaw+155°, 약간 차갑게) — 3점조명의 fill. 반대 각도에서 어두운 면 채움.
    4. **`LavaLights`**(강 경로 따라 포인트라이트 ~20개, 주황, 중간높이 range 26·intensity 11) — 동기부여된 용암 발광.
    - ⚠️ 지형이 런타임 절차생성이라 **라이트맵 베이크(GI 바운스)는 안 씀**. 라이트/옵시디언은 RiverPoint 기반 1회 배치 — riverPath 바꾸면 재생성 필요.
- ⚠️ **리스폰/순간이동 버그(중요)**: 테이블은 **빈 부모(TablePlayer) + 동적 Rigidbody 자식들**이라 위치 이동은 **각 Rigidbody의 `rb.position/rb.rotation`을 직접** 설정해야 함(부모 Transform이나 자식 `transform.position`만으론 동적 바디가 안 옮겨짐 — 물리 위치가 우선). MCP로 옮길 때도 `rb.position` 직접 설정.
- **리스폰/리셋(중요, 3종 분리)**: `SceneReset`은 Start에서 초기 위치 + **상판(bodies[0]) 기준 각 강체 상대배치(relPos/relRot)** + 상판의 지면 위 높이(boardClearance) 기록, `SnapInitialToGround()`로 초기위치를 지면에 스냅. 세 가지 복원:
  - **`1`키 = `ResetPoseInPlace()`**: 처음 위치로 안 감 — **현재 XZ 유지** + **테이블 forward 를 카메라가 보는 방향으로 정렬**해 똑바로(상대배치 재구성, 그 자리 지면에 안착). 다리 내부상태·지라프 다리길이도 초기화. ⚠️ **카메라 정렬이 핵심**: 기울어진 상태의 `eulerAngles.y` 는 부정확해 다리-카메라 매핑이 틀어짐 → `Camera.main.forward`(수평) 기준 yaw 로 세워야 **리셋 후 항상 WASD 다리(Leg_FL, 로컬 -X·+Z)가 화면 먼왼쪽**에 옴. (레그 배치: FR=TFGH·FL=WASD·BR=Arrows·BL=IJKL.)
  - **용암 사망(접촉 기반) = `JungleStage`**: `FixedUpdate`가 테이블 **콜라이더**들을 돌며 `TouchesLava(col)`(용암 리본 반폭 `LavaHalfWidth`=width/2 안 + 콜라이더 `bounds.min.y ≤ 용암표면 + lavaTouch(0.35)`) → **실제 용암에 닿으면** Respawn. (긴 다리로 높이 건너면 안 죽음.)
    - ★ **용암표면 = 실제 리본 기준**: `LavaSurfaceY = HeightAt(NearestRiverPoint) + lava.lift + lava.transform.position.y`. ⚠️ 표면 높이는 **테이블 발밑 지형이 아니라 강 중심선(=용암이 실제 있는 좁은 협곡 바닥)** 으로 잰다(`IslandTerrain.NearestRiverPoint`). 또 LavaRiver 리본은 메시 로컬Y(HeightAt+lift)에 **transform.y 오프셋**까지 더해 떠 있으므로(실제 width≈3.46·lift1·transform.y≈4.51 → 표면 ≈ HeightAt+5.5) 그 오프셋을 포함해야 보이는 용암과 일치. 예전엔 `HeightAt(테이블위치)+lift`만 써서 실제 표면보다 ~4.5 아래라 **닿아도 안 죽고 가라앉아야** 죽었음 → 수정. 라바 오브젝트를 옮기면 판정도 따라감.
  - **리스폰은 '원래 있던 쪽'**: `island.SignedSideOfRiver` 로 강 좌/우 부호(+1/-1). 안전할 때(협곡 밖) `lastSafeSide` 기록 → 빠지면 **그 쪽**의 가장 가까운 협곡 밖 평평한 고지대(`DistanceToRiver>CanyonRimHalf+4.5` & `H>물+2` & 경사<0.28)에 `ResetPoseAtXZ`. **건너편 땅엔 안 생김**(같은 쪽에 없으면 쪽 제약 풀고 재시도). (검증: +1쪽에서 협곡 투입→+1쪽 (5,11,40) 평지 복원.)
  - **`ResetAll()`**: 초기(스냅된) 위치로 텔레포트 — 현재 미사용이나 공개 API 유지.
  - 공통: 동적 바디라 `rb.position/rotation` 직접 설정. `GroundYAt`은 **y=500에서 내리쏴** 도랑 안에서도 정확.
- ⚠️ MCP Play 검증 시 에디터가 **백그라운드면 물리 정지**(Run In Background OFF) → `Application.runInBackground=true` 설정 후 검증.
- **분화구 용암 호수**: 화산은 `IslandTerrain`에서 **평평한 바닥의 분화구 웅덩이**로 성형(craterDepth 6, craterRadius 14 → 안쪽 0.65R 평탄 바닥, 림이 솟음). `LavaRiver`가 그 바닥에 **용암 호수**(craterPoolRadius 12, craterPoolLift 4)를 앉히고 림을 넘어 강으로 **넘쳐흐름**(craterBlend 0.13 구간 상승). ⚠️ 용암 메시는 LavaRiver **transform.position.y(=2.66, 사용자가 강 높이로 올림)** 를 코드에서 보정해 분화구 부분만 정확히 바닥에 박음 → 강 전체를 Y로 올려도 분화구는 안 뜸. 씬 YAML에 직렬화됨(파일이 소스, 코드 기본값 아님 — 씬이 Unity 저장본이라 컴포넌트 값 전부 직렬화됨).
- ⚠️ **Unity 직렬화 함정**: 씬에 이미 직렬화된 public 필드는 **코드 기본값을 바꿔도 안 바뀜**(직렬화값 우선). 라이브 변경은 MCP `set_component_property`/RunCommand로, 파일 반영은 씬 YAML 직접 수정 또는 해당 필드가 YAML에 없으면 코드 기본값 사용.
- 신규 guid: OceanWater.shader …9001 / Lava.shader …9002 / OceanWater.cs …9003 / IslandTerrain.cs …9005 / LavaRiver.cs …9006 / VertexColorLit.shader …9007 / Ocean.mat …9011 / Lava.mat …9012 / GiraffeSkin.mat …9015 / Island.mat …9017. (Sand/Plain.mat …9013/9014는 현재 미사용.)
- 스크립트 guid: HallwayStage f6a7b8c9.. / 씬 guid: Hallway 33445566..
- ✅ **이제 모든 씬의 테이블 = Table 프리팹 인스턴스**(과거 SampleScene/Stage1/Stage2는 독립 복사본이었으나 **프리팹 인스턴스로 교체 완료**). 따라서 **`Table.prefab` 수정이 6개 씬 전부에 자동 반영**됨(인스턴스가 오버라이드 안 한 값만). 양쪽 따로 수정 불필요. ⚠️ 프리팹 **루트 GO 이름은 "Table"**(보드도 "Table") → 인스턴스는 루트를 **"TablePlayer"로 rename**(오버라이드)해서 `GameObject.Find("Table")`가 보드(Rigidbody 보유)를 찾게 함. 새 인스턴스 만들면 반드시 루트 rename 할 것.
- **다리 힘**: `LegController.motorForce`(다리가 땅을 미는 토크)가 "움직임 세기"의 핵심 레버. 프리팹에서 60→**80**(전 씬 반영). 마찰(HighFriction 1.0/1.0 Maximum, bounce 0)은 이미 최대라 더 올리면 발이 걸려 넘어짐 → 손대지 말 것. swingVelocity 300, mass 보드3/다리1, linearDamping 0.
- ⚠️ **물리 폭발("Invalid worldAABB"/튕겨나감) 방지**: motorForce를 100으로 너무 올리면 **잠긴 다리의 하드락을 세게 때려 솔버 발산** → 위치가 거대 유한값으로 튀어 worldAABB 에러. 대응: ① `LegController.StabilizeBody`에 **`maxLinearVelocity=30`**(선속도 상한 = 발산 차단, 핵심) ② `TableSafety`가 Inf/NaN뿐 아니라 **거대 유한 위치(>MaxDist 5000)도 폭발로 간주해 복구** ③ motorForce 80으로 완화. (검증: 1e6 위치 주입→정상 복구.) **더 세게 원하면 maxLinearVelocity 함께 올리며 테스트.**
- **Hallway 조사 메모**: Hallway 환경(벽/바닥/의자)은 `HallwayStage`가 **런타임 생성** → ⚠️ **Edit 모드엔 안 보임**(IsPlaying=true 확인 후 검사할 것). 실제 Play에서 콜라이더 13개(Floor/Wall/Trap/Chair) 정상. 위 안전장치 적용 후 **모터 force200/vel400 + 벽 박치기 스트레스에도 worldAABB 폭발 재현 안 됨**(에러 0). 추가 보강: **벽 두께 0.4→1.0**(안쪽 면 유지, 바깥으로 wo=0.3 이동)으로 얇은 벽 관통 예방. ⚠️ 벽 마찰: 테이블 HighFriction(combine=Maximum 1.0)이라 벽도 1.0으로 그립 → 벽 타고 오르는 느낌 가능. 슬립 원하면 테이블 combine=Minimum + 바닥에 HighFriction + 벽에 슬립 머티리얼 필요(광범위 변경, 보류).

## 일시정지 메뉴 / 다리 색(X-ray)
- **PauseMenu** (`Assets/Scripts/PauseMenu.cs`): `ESC` 토글로 일시정지(`Time.timeScale=0`). IMGUI로 그리되 **클릭/슬라이더는 `Mouse.current`로 직접 처리**(New Input System 전용이라 `GUI.Button` 클릭이 안 먹음). 메뉴(영어): Resume / Volume 슬라이더(`AudioListener.volume`) / Color ON·OFF / Main Menu(StartScreen). **버튼·슬라이더가 아닌 바깥 아무데나 클릭하면 창이 닫힘**(모든 씬). ⚠️ 메뉴를 연 그 프레임의 클릭은 무시(`openedFrame` 가드) — 표지판/ESC로 연 클릭이 곧바로 '바깥 클릭'으로 처리돼 즉시 닫히는 것 방지. 외부에서 열기: `OpenMenu()`(StartScreen 옵션 표지판). 시작 화면 additive 배경에선 비활성(`gameObject.scene != activeScene` 가드).
- ⚠️ `ESC`는 PauseMenu 전용. **CameraFollow의 ESC 커서토글은 제거**(충돌 방지). 일시정지=커서 표시, 복귀=커서 Locked(마우스 시점 복원).
- **LegColorXray** (`Assets/Scripts/LegColorXray.cs`): 다리 4개(Leg_FR/FL/BR/BL)를 이름으로 찾아 박스 12모서리를 그림. **모서리마다 독립된 2점 LineRenderer**(다리당 12개) — 한 붓 그리기 경로의 "꼬임" 방지. 색 FR=빨강·FL=초록·BR=파랑·BL=노랑(기본 off, 메뉴에서 토글). 선명한 단색 선이며, X-ray는 `Assets/Shaders/XrayLine.shader`(ZTest Always, queue Overlay)로 몸통을 통과해 항상 화면 위에 보임. 다리는 씬마다 별도 루트라 **이름 전역 검색**(`GameObject.Find`)으로 찾음.
- 두 컴포넌트는 `Table.prefab`의 TablePlayer 루트(fileID 9100000010/9100000020)에 부착 → 모든 씬이 프리팹 인스턴스라 자동 상속(이제 씬별 별도 부착 불필요).

## 테이블 기능 (Features) — "평소 꺼짐, 특정 상태에서 켜짐" 패턴
- **패턴**: 기능 = TablePlayer 루트에 붙는 컴포넌트 + `public bool active`(기본 false). 평소엔 Update가 즉시 return(무동작). 게임 상태/이벤트가 `active=true`로 켜면 동작. 앞으로 기능 늘릴 땐 이 형식으로 추가(프리팹 = 전 씬 공유, off라 무해). 테스트는 SampleScene에서.
- **GiraffeMode** (`Assets/Scripts/GiraffeMode.cs`, guid ..9008, 프리팹 fileID **9100000050**): 켜지면 **각 다리를 키로 늘리고 줄임**(기린처럼 키 큰 테이블). 다리 메시(Leg_FR 등)의 `localScale.y`(초기 0.7=최소 ~ ×`maxMultiplier`(**7**)=4.9 최대)를 `speed`(**0.8**/초)로 바꾸고 `localPosition.y=-len/2`(위를 피벗에 고정→아래로 신장). 다리 메시는 피벗 Rigidbody 자식이라 콜라이더가 길어져 **발이 땅 밀어 몸이 들림**(순수 물리). 꺼지면 초기 길이로 복귀. (검증: 4다리 7배→board y+4.2까지 수평 안정 상승.)
- ⚠️ **핵심 튜닝(중요)**: 신축 `speed`가 너무 빠르면(2.5) 발이 디페네트레이션(폭발방지 maxDepenetrationVelocity=3)보다 빨리 내려가 **바닥 관통→테이블 주저앉음**. **speed를 낮춰(0.8)** 디페네트레이션이 몸을 들어올리는 속도에 맞추니 7배도 안정적으로 키 커짐. (max를 낮추는 게 아니라 speed가 관건.) 더 빠른 신축 원하면 speed↑ 하되 관통 안 하는 선까지만.
  - 키: FR(R짧게/Y길게) · FL(Q/E) · BR(P/`]`) · BL(U/O). **토글 = `2`(Digit2, `toggleKey`)**. ⚠️ 이 키들은 이동/보조키와 충돌 없음.
  - **켜져 있을 때 좌상단에 작은 기린 아이콘 표시**(`OnGUI`). ⚠️ IMGUI 기본 폰트가 컬러 이모지(🦒)를 못 그려서 — `MakeGiraffeIcon()`으로 **작은 기린을 Texture2D에 직접 픽셀로 그려** `GUI.DrawTexture`(scale 3.0). 이모지 글자 대신 그린 아이콘.
  - ⚠️ **다리 관성 안정화 `LockInertia()`** (Start+길이변경+ResetLegs 시 호출): **현재 길이 기준 자연 관성**으로 재계산 — `ResetInertiaTensor()`+`ResetCenterOfMass()` 후 **0근처 성분만 minI(0.05) 클램프** + maxAngularVelocity 20 상한.
    - ⚠️ **(과거 버그)** 예전엔 초기(짧은 다리) 관성 `(0.04, 0.00, 0.04)` 를 긴 다리에 강제했는데 — **Y(긴축) 성분이 0** 이라 솔버가 `1/I` 로 폭주→ **`Assertion failed: IsFinite(distanceAlongView/distanceForSort)` NaN 다발**, 또 관성이 너무 작아 긴 다리가 무게를 못 버텨 **idle 다리가 축 처짐**. → 자연 관성(긴 다리=큰 관성=안정)+클램프로 둘 다 해결. (검증: 4× 늘린 idle 다리 6초 후 up.y=1.00 수평·angVel 0·NaN 없음; 모터+비틀림 구동도 안정.)
    - 모터는 '목표 각속도' 구동이라 관성이 커도 **스윙 속도(체감)는 동일**(가속만 부드러워져 들썩임도 오히려 완화).
    - ⚠️ **관성 최대는 제한하지 않음**(min 클램프만). 한때 `maxI=0.4`로 깎았더니 — 긴 지렛대(긴 다리)가 가벼워져 **미세 입력·접촉에 과민반응**, 특히 *뒤집힌 채 흔들면* 큰 힘 받아 폭주. 무거운 자연 관성이 외력에 안정적이라 max 제거.
  - ⚠️ **다리 각속도 길이 반비례 (`LegController.cs` lenScale)**: 모터 `swingVelocity`/`spinSpeed`에 `lenScale = clamp(기본길이/현재길이, 0.12, 1)` 를 곱함 → 긴 다리는 천천히 회전해 **발끝 선속도가 일반 다리와 동일**. 안 하면 긴 다리가 일반 각속도(300°/s)로 돌아 **각운동량 폭증→한계각 오버슈트·정지다리 접힘**. 일반 모드는 lenScale=1이라 무영향. (검증: 4× 다리 52°/s 구동→angle 55°(±90 안)·Y비틀림 충격에도 폭주/ NaN 없음.)
  - **`1` 리셋 시 다리 길이도 초기화**: `SceneReset.ResetAll`이 `GiraffeMode.ResetLegs()` 호출(메시 스케일/위치 base 복원 + 관성 재고정).
  - ★ **기린 무늬 스킨**(켜질 때 몸통+다리, **UV+커스텀 메시**): `MakeGiraffeTexture()`로 그물무늬(reticulated) 기린 텍스처 절차 생성(타일 가능 Voronoi, 크림 선 사이 갈색 패치, G=3). URP Lit 머티리얼(몸통/다리 별도)로 적용, 끌 때 원복. (※ 삼평면 셰이더 방식은 사용자가 반려 → UV 방식 안에서 해결.)
    - **상판 옆면↔윗면 연속**: `BuildCrossNetBox(lossyScale, patternScale)` — 상판을 **십자전개(cross-net) UV 박스 메시**로 교체. UV를 월드 위치로 매핑하되 **옆면을 윗면 모서리에서 아래로 펼쳐(접힘)** 윗면 무늬가 모서리 넘어 옆면으로 자연스럽게 이어짐(평평한 옆 패턴 X). `patternScale`(타일/단위, 0.6).
    - **다리**: `UniformLegBox`(모든 옆면 V=길이 Y인 균일 UV 박스)로 교체 + `UpdateLegTiling()`이 **`legSpotWorld`(0.08m) 정사각 점**이 되도록 타일 설정(tiling=(w/(legSpotWorld·G), len/(legSpotWorld·G))) → 신장해도 점 크기 일정. ⚠️ 다리가 얇아(0.12) 몸통과 같은 점 크기면 면이 단색이 되므로 다리 점은 약간 작게(또렷). 튜닝: `patternScale`(몸통)·`legSpotWorld`(다리, 작을수록 점 작아짐/촘촘). 튜닝: `bodyTile/legTileX/legTileBaseY`. ⚠️ **함정**: 텍스처 생성에 `Mathf.SmoothStep(0,0.05,edge)` 쓰면 from→to 보간이라 전부 크림됨 — 자체 `SStep(e0,e1,x)`(GLSL식) 써야 함.
  - ⚠️ MCP revoked 상태에서 **프리팹 파일 직접 편집**으로 부착함(컴포넌트 9100000050 + m_Component 등록). Unity 포커스 시 리임포트되어 전 씬 반영. Samplescene 전용으로 옮기려면 MCP 켜고 인스턴스 added-component로 이동.

## ★ 세션 인계 가이드 (새 대화에서 먼저 읽기)
### 작업 규칙 (중요)
- 변경은 **에이전트가 파일을 직접 편집** → 사용자가 Unity에서 **저장 말고 reload(Don't Save)** 로 반영. (에디터에서 저장하면 파일 편집이 덮어써질 수 있음)
- 씬/프리팹은 손으로 YAML 작성하거나 Python으로 생성. 편집 후 파이썬으로 무결성 검증(미해결 fileID 참조 0 확인).
- UI는 가능하면 **uGUI(Canvas) 또는 런타임 생성/3D**. EventSystem 회피 위해 클릭은 마우스 위치로 직접 감지(새 Input System).
- 입력 처리: **New Input System 전용**(activeInputHandler=1). 옛 `Input.GetAxis` 금지, `Keyboard.current/Mouse.current` 사용.

### 조작/게임 요약
- 다리 4개: WASD / TFGH / IJKL / 방향키. 상하=앞뒤 스윙(모터), 좌우=긴축(Y) 회전(+스윙축 동조). 자기 키 없으면 힌지 잠금.
- **다리↔키 매핑(프리팹 기준, 전 씬 인스턴스 상속)**: Leg_FR=**TFGH** / Leg_FL=**WASD** / Leg_BR=**Arrows(+보조키)** / Leg_BL=**IJKL**. (과거 FR=WASD/FL=TFGH/BR=IJKL/BL=Arrows 에서 WASD↔TFGH, IJKL↔Arrows 두 쌍을 교환함.)
- **Arrows 다리(현재 Leg_BR_Pivot)는 보조 키로도 조작**: `[`=위, `'`=아래, `;`=왼쪽, 오른쪽 `Enter`=오른쪽. `LegController`에 `altUp/altDown/altLeft/altRight`(Key, 기본 None) 필드 → 설정된 키를 기존 입력에 OR. **프리팹에만 설정**(Arrows 다리)되어 전 씬 상속. 키 교환 시 보조키는 Arrows 세트를 따라 이동시킴.
- `1`키: 테이블 초기 위치 리셋(SceneReset).
- 카메라(`CameraFollow`): 마우스로 시점 회전 + **휠로 줌(distance 2.5~14)**. SampleScene/Stage1/Stage2/Jungle 공통. (StartScreen=메뉴 고정, Hallway=컷신 → CameraFollow 없음)
- 홈 버튼 4개 다 누르면 CLEAR(3D 텍스트, 서서히 커짐, 유지).

### 주요 에셋 guid
- Table.mat 5e9a... / Button.mat c9d0e1f2... / HighFriction.physicMaterial e7f8a9b0...
- 스크립트: LegController c5d6e7f8.. / SceneReset a3b4c5d6.. / CameraFollow a3f1c2d4.. / FlyoverCamera e1f2a3b4.. / StartMenu f2a3b4c5.. / ClearChecker(파일명 동일) / PressButton b8c9d0e1.. / PauseMenu c2d3e4f5.. / LegColorXray b1c2d3e4.. / XrayLine.shader d3e4f5a6.. / TitleWobble(시작화면 타이틀 글자 흔들림) / ThoughtReveal(Hallway 상상 말풍선 순차등장+페이드아웃)
- Table.prefab 0a1b2c3d.. / PressButton.prefab a7b8c9d0..
- 씬 guid: SampleScene 99c97208.. / StartScreen 11223344.. / Stage1 1111..aaaa / Stage2 2222..bbbb

### fileID 대역 관습(씬 내)
- 1500000xxx=Ground, 1600000xxx=Table/다리, 1600009xxx=Table 루트의 PauseMenu(9010)/LegColorXray(9020), 1700000xxx=플랫폼/램프, 1800000xxx=버튼, 1900000xxx=ClearChecker, 1950000xxx=StartMenu, 2000000xxx=Canvas/UI.
