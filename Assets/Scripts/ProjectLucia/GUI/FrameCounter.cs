using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 화면에 현재 프레임 속도(FPS)와 프레임 타임(ms)을 표시하는 디버그용 클래스입니다.
    /// OnGUI를 사용하여 화면 좌상단에 텍스트를 렌더링합니다.
    /// </summary>
    public class FrameCounter : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("FPS 텍스트의 폰트 크기")]
        [SerializeField] private int size = 25;

        [Tooltip("FPS 텍스트의 색상")]
        [SerializeField] private Color color = Color.red;

        #endregion

        #region Public Fields (공개 필드)

        /// <summary>
        /// 디버그 모드 활성화 여부 (true일 때만 FPS 표시)
        /// </summary>
        [HideInInspector] public bool isDebug;

        #endregion

        #region Private Fields (비공개 필드)

        /// <summary>
        /// 프레임 간 시간 차이를 누적하여 평균을 내기 위한 변수
        /// </summary>
        private float _deltaTime;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        void Update()
        {
            // 디버그 모드일 때만 델타 타임 계산 (부드러운 값 변화를 위해 보간 사용)
            if (isDebug)
            {
                _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            }
        }

        private void OnGUI()
        {
            // 디버그 모드가 아니면 렌더링하지 않음
            if (!isDebug) return;

            GUIStyle style = new GUIStyle();

            // 텍스트 표시 영역 및 스타일 설정
            Rect rect = new Rect(30, 30, Screen.width, Screen.height);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = size;
            style.normal.textColor = color;

            // FPS 및 ms 계산
            float ms = _deltaTime * 1000f;
            float fps = 1.0f / _deltaTime;
            
            // 출력 문자열 포맷팅
            string text = $"{fps:0.} FPS ({ms:0.0} ms)";

            // 화면에 라벨 표시
            UnityEngine.GUI.Label(rect, text, style);
        }

        #endregion
    }
}
