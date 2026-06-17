using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// 시작 화면. 고정 카메라 + 흰 배경. 테이블은 씬에 배치된 Table 프리팹 인스턴스(편집 모드에서도 보임).
/// 테이블이 디딜 단단한 흰 바닥을 런타임 생성하고, 왼쪽 PLAY 버튼 클릭 시 게임 씬을 로드한다.
/// 키보드로 게임과 동일하게 조작 가능(프리팹 인스턴스의 LegController/물리 그대로,
/// PauseMenu·LegColorXray는 프리팹 인스턴스 수정으로 비활성).
/// 클릭은 EventSystem 없이 마우스 위치로 직접 감지. 커서는 항상 보이게 강제.
/// </summary>
public class StartMenu : MonoBehaviour
{
    [Tooltip("PLAY 시 로드할 게임 씬")]
    public string playScene = "SampleScene";

    [Header("UI")]
    public RectTransform playButton;

    void Start()
    {
        BuildGround();
        Unlock();
    }

    // 테이블이 디딜 단단한 흰 바닥(박스). 윗면이 y=0. 그림자는 받되 던지지 않음.
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
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Vector2 p = mouse.position.ReadValue();
        if (playButton != null && RectTransformUtility.RectangleContainsScreenPoint(playButton, p, null))
            SceneManager.LoadScene(playScene, LoadSceneMode.Single);
    }
}
