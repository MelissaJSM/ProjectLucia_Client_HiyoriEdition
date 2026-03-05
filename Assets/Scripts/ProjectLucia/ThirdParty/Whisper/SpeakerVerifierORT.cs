using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectLucia.ThirdParty.Whisper
{
    /// <summary>
    /// ONNX Runtime을 사용하여 화자 인식(Speaker Verification)을 수행하는 클래스입니다.
    /// VoxCeleb 모델을 사용하여 입력된 오디오가 등록된 화자(주인님)인지 검증합니다.
    /// </summary>
    public class SpeakerVerifierORT : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("1. Model Settings")]
        [Tooltip("화자 인식에 사용할 ONNX 모델 파일명 (StreamingAssets/Vad 폴더 내)")]
        public string modelFileName = "voxceleb.onnx"; 
        
        [Header("2. Data Folders")]
        [Tooltip("등록된 화자(주인님)의 목소리 샘플 리스트 (StreamingAssets/Vad/DetectVoice/Positive 폴더에서 로드됨)")]
        public List<AudioClip> referenceClips = new List<AudioClip>();

        [Tooltip("타인/일반인 목소리 샘플 리스트 (StreamingAssets/Vad/DetectVoice/Negative 폴더에서 로드됨)")]
        public List<AudioClip> negativeClips = new List<AudioClip>();

        [Header("3. Threshold")]
        [Tooltip("화자 인식 성공을 위한 최소 유사도 임계값 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float similarityThreshold = 0.7f; // 블랙리스트 적용 시 0.7 추천

        #endregion

        #region Private Fields (비공개 필드)

        private InferenceSession _session;
        private readonly List<float[]> _masterEmbeddings = new List<float[]>();   
        private readonly List<float[]> _negativeEmbeddings = new List<float[]>(); 
        private bool _isReady;
        private readonly object _lock = new object(); // 멀티스레드 안전성을 위한 락

        #endregion

        #region Public Properties

        /// <summary>
        /// 모델과 데이터가 모두 로드되어 추론 가능한 상태인지 여부
        /// </summary>
        public bool IsReady => _isReady;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        void OnDestroy()
        {
            UnloadModel();
        }

        #endregion

        #region Initialization & Cleanup (초기화 및 해제)

        /// <summary>
        /// 모델을 로드하고 화자 데이터를 초기화합니다. (Async)
        /// </summary>
        public async Task InitModel()
        {
            if (_isReady) return;

            // 1. 모델 로드
            string modelPath = Path.Combine(Application.streamingAssetsPath,"Vad", modelFileName);
            try 
            {
                await Task.Run(() => 
                {
                    lock (_lock)
                    {
                        if (_session == null) _session = new InferenceSession(modelPath);
                    }
                });
                
                if(SettingData.IsDebug) Debug.Log($"✅ [Verifier] AI 모델 로드 완료: {modelFileName}");
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogError($"❌ [Verifier] 모델 로드 실패: {e.Message}");
                return;
            }

            // 2. 데이터 로드 (Positive & Negative)
            await LoadClipsFromFolderAsync(AudioCategory.Positive, referenceClips, _masterEmbeddings);
            await LoadClipsFromFolderAsync(AudioCategory.Negative, negativeClips, _negativeEmbeddings);

            lock (_lock) _isReady = true;
            
            if (_masterEmbeddings.Count > 0) 
            {
                if(SettingData.IsDebug) Debug.Log("🚀 화자 인식 준비 완료!");
            }
            else
            {
                if(SettingData.IsDebug) Debug.LogWarning("⚠️ 주인님 목소리(Positive)가 없습니다! 화자 인식을 건너뜁니다.");
            }
        }

        /// <summary>
        /// 모델 세션을 닫고 메모리를 정리합니다.
        /// </summary>
        public void UnloadModel()
        {
            lock (_lock)
            {
                _isReady = false;
                
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }
                
                _masterEmbeddings.Clear();
                _negativeEmbeddings.Clear();
                referenceClips.Clear();
                negativeClips.Clear();
            }
            if(SettingData.IsDebug) Debug.Log("💤 [Verifier] 모델 언로드 완료");
        }

        /// <summary>
        /// 지정된 폴더에서 WAV 파일을 로드하여 임베딩을 추출하고 리스트에 추가합니다.
        /// </summary>
        private async Task LoadClipsFromFolderAsync(AudioCategory category, List<AudioClip> clipList, List<float[]> embeddingList)
        {
            // 리스트 초기화는 메인 스레드에서 안전하게 수행 (InitModel 호출 시점에는 경합 없음 가정)
            clipList.Clear();
            lock(_lock) embeddingList.Clear();
            
            string basePath = Path.Combine(Application.streamingAssetsPath, "Vad", "DetectVoice", category.ToString());
            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

            string[] files = Directory.GetFiles(basePath, "*.wav");
            foreach (string filePath in files)
            {
                string normalizedPath = filePath.Replace("\\", "/");
                string url = "file://" + normalizedPath;
                using var www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
                var op = www.SendWebRequest();

                while (!op.isDone) await Task.Yield();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    clip.name = Path.GetFileNameWithoutExtension(filePath);
                    clipList.Add(clip);

                    // 로드 즉시 임베딩 변환 및 등록
                    // ExtractEmbeddingFromClip 내부에서 _session을 사용하므로 락 필요?
                    // _session.Run은 thread-safe하지만, UnloadModel과의 경합 방지
                    float[] emb = null;
                    lock (_lock)
                    {
                        if (_session != null)
                            emb = ExtractEmbeddingFromClip(clip);
                    }
                    
                    if (emb != null) 
                    {
                        lock(_lock) embeddingList.Add(emb);
                    }
                }
            }
            if(SettingData.IsDebug) Debug.Log($"📂 [{category}] {embeddingList.Count}개 등록 완료");
        }

        #endregion

        #region Verification Logic (검증 로직)

        /// <summary>
        /// 입력된 오디오 데이터를 실시간으로 검증하여 화자 일치 여부를 반환합니다.
        /// </summary>
        /// <param name="audioData">오디오 PCM 데이터</param>
        /// <param name="finalScore">계산된 최종 유사도 점수 (out)</param>
        /// <returns>화자 일치 여부 (true/false)</returns>
        public bool VerifyUser(float[] audioData, out float finalScore)
        {
            finalScore = 0f;
            
            lock (_lock)
            {
                // 데이터가 너무 짧거나(0.5초 미만) 모델 로드 안됐으면 패스
                if (!_isReady || _session == null || audioData.Length < 8000) return false;

                // 0. 주인님 목소리가 등록되지 않았으면 무조건 통과 (기능 Skip)
                if (_masterEmbeddings.Count == 0)
                {
                    finalScore = 1.0f;
                    return true;
                }

                // 1. 입력 오디오 임베딩 추출
                float[] currentEmb = ExtractEmbedding(audioData);
                if (currentEmb == null) return false;

                // 2. 주인님 유사도 (최대값)
                float maxPosSim = 0f;
                foreach (var refEmb in _masterEmbeddings)
                {
                    float sim = ComputeCosineSimilarity(refEmb, currentEmb);
                    if (sim > maxPosSim) maxPosSim = sim;
                }

                // 3. 타인 유사도 (최대값)
                float maxNegSim = 0f;
                if (_negativeEmbeddings.Count > 0)
                {
                    foreach (var negEmb in _negativeEmbeddings)
                    {
                        float sim = ComputeCosineSimilarity(negEmb, currentEmb);
                        if (sim > maxNegSim) maxNegSim = sim;
                    }
                }

                // 4. 점수 보정 (블랙리스트 전략)
                // 타인과 더 비슷하면 가차 없이 0점
                if (maxNegSim >= maxPosSim)
                {
                    finalScore = 0f; 
                }
                else
                {
                    // 주인 점수에서 타인 점수의 절반을 뺌 (변별력 강화)
                    finalScore = maxPosSim - (maxNegSim * 0.5f);
                }
            }

            return finalScore >= similarityThreshold;
        }

        /// <summary>
        /// 비동기로 화자 인식을 수행합니다. (메인 스레드 블로킹 방지)
        /// </summary>
        public async Task<(bool isVerified, float score)> VerifyUserAsync(float[] audioData)
        {
            // 락 없이 빠른 체크 (정확성은 떨어져도 됨)
            if (!_isReady) return (false, 0f);

            // 배열 복사 (스레드 안전성)
            float[] dataCopy = new float[audioData.Length];
            Array.Copy(audioData, dataCopy, audioData.Length);

            return await Task.Run(() =>
            {
                bool result = VerifyUser(dataCopy, out float score);
                return (result, score);
            });
        }

        #endregion

        #region Internal Utilities (내부 유틸리티)

        private float[] ExtractEmbeddingFromClip(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            if (clip.channels > 1) samples = ConvertToMono(samples, clip.channels);
            return ExtractEmbedding(samples);
        }

        private float[] ExtractEmbedding(float[] audioData)
        {
            try
            {
                // ONNX 입력: [Batch: 1, Time: N]
                var inputTensor = new DenseTensor<float>(
                    new Memory<float>(audioData), 
                    new[] { 1, audioData.Length }
                );
                
                // 모델 입력 이름 "audio_input"
                var inputs = new List<NamedOnnxValue> { 
                    NamedOnnxValue.CreateFromTensor("audio_input", inputTensor) 
                };

                using var results = _session.Run(inputs);
                // 모델 출력 이름 "embedding_output"
                var outputTensor = results.First().AsTensor<float>();
                return NormalizeVector(outputTensor.ToArray());
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogError($"추론 에러: {e.Message}");
                return null;
            }
        }

        private float ComputeCosineSimilarity(float[] vecA, float[] vecB)
        {
            float dot = 0f;
            for (int i = 0; i < vecA.Length; i++) dot += vecA[i] * vecB[i];
            return dot;
        }

        private float[] NormalizeVector(float[] vec)
        {
            float sumSquares = 0f;
            foreach (var t in vec)
                sumSquares += t * t;

            float magnitude = Mathf.Sqrt(sumSquares);
            if (magnitude < 1e-6f) return vec;
            for (int i = 0; i < vec.Length; i++) vec[i] /= magnitude;
            return vec;
        }

        private float[] ConvertToMono(float[] rawSamples, int channels)
        {
            float[] mono = new float[rawSamples.Length / channels];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++) sum += rawSamples[i * channels + ch];
                mono[i] = sum / channels;
            }
            return mono;
        }

        #endregion
    }
}