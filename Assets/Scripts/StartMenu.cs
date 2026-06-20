using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// 시작 화면. 고정 카메라 + 흰 배경. 테이블은 씬에 배치된 Table 프리팹 인스턴스(편집 모드에서도 보임).
/// 메뉴는 3D **나무 표지판**(Sign_Play / Sign_Option)을 마우스로 클릭:
///   - Play 표지판 → playScene(Hallway) 로드
///   - Option 표지판 → ESC와 동일한 옵션 메뉴(PauseMenu) 열기
/// 클릭은 EventSystem 없이 카메라 레이캐스트(콜라이더)로 감지. 커서는 항상 보이게 강제.
/// </summary>
public class StartMenu : MonoBehaviour
{
    [Tooltip("Play 표지판 클릭 시 로드할 게임 씬")]
    public string playScene = "Hallway";
    [Tooltip("Play 표지판 GameObject 이름")] public string playSignName = "Sign_Play";
    [Tooltip("Option 표지판 GameObject 이름")] public string optionSignName = "Sign_Option";

    Camera cam;
    PauseMenu pauseMenu;

    void Start()
    {
        // StartGround는 이제 씬에 직접 배치된 편집 가능한 오브젝트다. 씬에 없을 때만 런타임 생성(폴백).
        if (GameObject.Find("StartGround") == null) BuildGround();
        Unlock();
        cam = Camera.main;
        pauseMenu = Object.FindAnyObjectByType<PauseMenu>();
    }

    // 테이블이 디딜 단단한 흰 바닥(박스). 윗면이 y=0. 그림자는 받되 던지지 않음.
    // (씬에 StartGround가 없을 때만 호출되는 폴백 — 평소엔 씬의 실제 오브젝트를 사용.)
    void BuildGround()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "StartGround";
        g.transform.position = new Vector3(0f, -0.5f, 0f);
        g.transform.localScale = new Vector3(60f, 1f, 60f); // 윗면 y=0

        var mr = g.GetComponent<MeshRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh != null)
        {
            var m = new Material(sh);
            m.color = new Color(0.95f, 0.95f, 0.96f);
            mr.sharedMaterial = m;
        }
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Unlock();

        // 옵션 메뉴가 열려 있으면 그 클릭은 PauseMenu가 처리 → 표지판 레이캐스트 안 함.
        if (pauseMenu != null && pauseMenu.IsOpen) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        // 표지판 루트 이름으로 어떤 표지판인지 판별(콜라이더가 표지판 루트에 있음).
        string n = hit.collider.gameObject.name;
        if (n == playSignName)
            SceneManager.LoadScene(playScene, LoadSceneMode.Single);
        else if (n == optionSignName && pauseMenu != null)
            pauseMenu.OpenMenu();
    }
}
