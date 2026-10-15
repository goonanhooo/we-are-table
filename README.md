# we are table.

Unity 게임과 이번에 만든 두 웹 버전을 함께 보관합니다.

- `Assets/`, `Packages/`, `ProjectSettings/`: 원본 Unity 프로젝트. Unity 6000.3.17f1에서 엽니다.
- `Web/SinglePlayer/`: Three.js와 Rapier로 다시 만든 로컬 싱글 플레이 버전. 폴더에서 `node serve.mjs`를 실행한 뒤 `http://127.0.0.1:4173/`을 엽니다.
- `Web/PhoneController/`: Unity 실행본의 다리를 폰으로 조작하는 중계 서버와 웹 화면. 폴더에서 `npm install` 후 `start.ps1`을 실행합니다. PC의 `http://127.0.0.1:4180/host`에 다리별 QR과 방 코드가 나옵니다.

멀리 떨어진 폰은 HTTPS 주소로 접속합니다. `Web/PhoneController/README.md`에 PC 함께하기, 관전 화면, 아이폰 자이로 보정과 임시 터널 실행 방법을 적었습니다. 실행 중 생성되는 방 코드와 호스트 키는 Git에 넣지 않습니다.

`Web/PhoneController/unity/`는 중계 프로그램만 따로 배포할 때 사용할 Unity 스크립트 사본입니다. 이 저장소의 Unity 프로젝트에는 같은 스크립트가 이미 `Assets/` 아래에 있습니다.
