using ProjectLucia.Status;
using UnityEngine;

namespace ProjectLucia.Capture
{
    /// <summary>
    /// 캡처된 이미지들을 사용하는 예제 클래스입니다.
    /// 갤러리 매니저에서 캡처된 텍스처들을 가져와 활용하는 방법을 보여줍니다.
    /// </summary>
    public class CaptureUseExample : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("캡처된 이미지들을 관리하는 갤러리 매니저 참조")]
        public CaptureGalleryManager galleryManager;

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 갤러리에 저장된 모든 캡처 이미지를 가져와 사용하는 예제 메서드입니다.
        /// 버튼 클릭 이벤트 등에 연결하여 사용할 수 있습니다.
        /// </summary>
        public void OnClickUseAllCaptures()
        {
            if (galleryManager == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("CaptureUseExample: GalleryManager가 할당되지 않았습니다.");
                return;
            }

            // 갤러리에서 모든 텍스처 가져오기
            var textures = galleryManager.GetAllTextures();

            if (textures.Count == 0)
            {
                if(SettingData.IsDebug) Debug.Log("CaptureUseExample: 저장된 캡처 이미지가 없습니다.");
                return;
            }

            // 가져온 텍스처들을 순회하며 작업 수행
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D tex = textures[i];
                if (tex == null) continue;

                // TODO: 여기에서 LLM 전송, OCR 분석, 파일 저장 등 실제 필요한 작업을 수행하세요.
                if(SettingData.IsDebug) Debug.Log($"[CaptureUseExample] 캡처 {i} 처리 중: 해상도 {tex.width}x{tex.height}");
            }
        }

        // 특정 인덱스의 이미지만 사용하고 싶을 때의 예제 (주석 처리됨)
        /*
        /// <summary>
        /// 특정 인덱스의 캡처 이미지만 가져와 사용하는 예제 메서드입니다.
        /// </summary>
        /// <param name="index">가져올 이미지의 인덱스</param>
        public void OnClickUseCaptureIndex(int index)
        {
            if (galleryManager == null) return;

            Texture2D tex = galleryManager.GetTextureAt(index);
            if (tex == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning($"CaptureUseExample: 인덱스 {index}에 해당하는 이미지가 없습니다.");
                return;
            }
        
            // TODO: tex를 LLM 입력, 다른 RawImage 표시, 파일 저장 등에 사용
            if(SettingData.IsDebug) Debug.Log($"[CaptureUseExample] 인덱스 {index} 이미지 사용: {tex.width}x{tex.height}");
        }
        */

        #endregion
    }
}
