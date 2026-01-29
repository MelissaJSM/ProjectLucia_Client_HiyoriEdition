using ProjectLucia.Status;
using TMPro;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 개별 GPU의 VRAM 사용량 및 정보를 UI에 표시하는 클래스입니다.
    /// 프리팹에 부착되어 리스트 형태로 생성될 때 사용됩니다.
    /// </summary>
    public class GpuVramItemUI : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("GPU ID를 표시할 텍스트 (예: GPU 0)")]
        [SerializeField] private TextMeshProUGUI vramText;

        [Tooltip("VRAM 사용량을 표시할 텍스트 (예: 2048 / 8192MB)")]
        [SerializeField] private TextMeshProUGUI vramValueText;

        [Tooltip("GPU 모델명을 표시할 텍스트")]
        [SerializeField] private TextMeshProUGUI gpuNameText;

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// GPU 정보를 받아 UI를 갱신합니다.
        /// 사용량이 80% 이상일 경우 텍스트 색상을 빨간색으로 변경하여 경고합니다.
        /// </summary>
        /// <param name="info">표시할 GPU 정보 객체</param>
        public void UpdateView(GpuInfo info)
        {
            // VRAM 사용률 계산
            float usagePercent = info.gpu_total > 0
                ? (float)info.gpu_used / info.gpu_total * 100f
                : 0f;

            // 80% 이상 사용 시 빨간색 경고
            var color = usagePercent >= 80f ? Color.red : Color.black;

            // UI 텍스트 업데이트
            if (vramText != null)
            {
                vramText.text = $"GPU {info.gpu_id}";
                vramText.color = color;
            }

            if (vramValueText != null)
            {
                vramValueText.text = $"{info.gpu_used} / {info.gpu_total}MB";
                vramValueText.color = color;
            }

            if (gpuNameText != null)
            {
                gpuNameText.text = info.gpu_name;
                gpuNameText.color = color;
            }
        }

        #endregion
    }
}
