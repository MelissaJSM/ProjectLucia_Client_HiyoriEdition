using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using StatusGpuInfo = ProjectLucia.Status.GpuInfo;
// ReSharper disable InconsistentNaming

namespace ProjectLucia.Status
{
    #region Request Data Structures (요청 데이터 구조)

    /// <summary>
    /// 일반적인 텍스트 대화 요청 데이터를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    class RequestData
    {
        [Tooltip("사용자가 입력한 텍스트")]
        public string text;

        [Tooltip("감정 분석 활성화 여부")]
        public bool emotion;
    }

    /// <summary>
    /// 피드백 전송 요청 데이터를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    class RequestDataFeedback
    {
        [Tooltip("사용자가 입력한 피드백 내용")]
        public string feedback; // text → feedback

        [Tooltip("피드백 대상 로그의 고유 번호 (ID)")]
        public int number; // feedbackID → number
    }

    #endregion

    #region Response Data Structures (응답 데이터 구조)

    /// <summary>
    /// 서버로부터 받은 대화 응답 데이터를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    class ServerResponse
    {
        [Tooltip("LLM이 생성한 텍스트 응답")]
        public string llm_response;

        [Tooltip("분석된 감정 상태 (예: Happy, Sad, Idle)")]
        public string emotion_analysis;

        [Tooltip("TTS 생성 결과 음성 파일 URL")]
        public string audio_url; 
    }

    /// <summary>
    /// 서버의 상태 정보를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    class ServerStatus
    {
        [Tooltip("서버 상태 문자열 (예: ok, error)")]
        public string status;
    }

    /// <summary>
    /// 서버 모니터링 응답 데이터를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    public class MonitoringResponse
    {
        [Tooltip("모니터링 상태")]
        public string status;

        [Tooltip("감지된 GPU 정보 목록")]
        public GpuInfo[] gpus;
    }

    /// <summary>
    /// 개별 GPU의 상태 정보를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    public class GpuInfo
    {
        [Tooltip("GPU 인덱스 ID")]
        public int gpu_id;

        [Tooltip("GPU 모델명")]
        public string gpu_name;

        [Tooltip("현재 사용 중인 VRAM 용량 (MB)")]
        public int gpu_used;

        [Tooltip("전체 VRAM 용량 (MB)")]
        public int gpu_total;
    }

    /// <summary>
    /// 이미지 업로드 결과 응답을 담는 클래스입니다.
    /// </summary>
    [Serializable] public class UploadImageResponse { public bool ok; public string image_id; } 

    
    /// <summary>
    /// 피드백 처리 결과 상세 내용을 담는 클래스입니다.
    /// </summary>
    [Serializable] public class FeedbackResult { public string op; public string result; }

    
    // [DTO Definition]
    [Serializable]
    public class ObservePayload
    {
        public string image_id;
        public string user_name;
    }
        
    // 알림 전송용 페이로드
    [Serializable]
    public class NotificationPayload
    {
        public string app_name; public string content; public string image_id; public string user_name;
    }

    [Serializable] public class ObserveResult 
    { 
        public string op; 
        public bool should_speak; 
        public string llm_response; 
        public string reason;
        public string emotion;        
        public string audio_filename; 
    }
        
    [Serializable] public class ChatResult { public string op; public string llm_response; public string emotion; public string audio_filename; }
    [Serializable] public class MonitoringPacket { public string op; public string status; public StatusGpuInfo[] gpus; }

    #endregion

    #region MySQL Status (MySQL 상태 정보)

    /// <summary>
    /// MySQL 데이터베이스 연결 상태 정보를 정의하는 클래스입니다.
    /// </summary>
    [Serializable]
    public class MysqlStatusInfo
    {
        [Tooltip("현재 연결 상태 (Open, Closed 등)")]
        public ConnectionState state;

        [Tooltip("상태 설명 메시지")]
        public string message;

        [Tooltip("UI 표시용 텍스트 색상")]
        public Color textColor;

        public MysqlStatusInfo(ConnectionState state, string message, Color textColor)
        {
            this.state = state;
            this.message = message;
            this.textColor = textColor;
        }

        /// <summary>
        /// 미리 정의된 상태별 정보 목록입니다.
        /// </summary>
        public static List<MysqlStatusInfo> statusInfos = new List<MysqlStatusInfo>
        {
            new(ConnectionState.Closed, "닫힘", Color.red),
            new(ConnectionState.Open, "열림", Color.green),
            new(ConnectionState.Connecting, "연결 중", Color.yellow),
            new(ConnectionState.Executing, "명령 실행 중", Color.black),
            new(ConnectionState.Fetching, "데이터 수신 중", Color.black),
            new(ConnectionState.Broken, "연결 손상됨", Color.red)
        };
    }

    #endregion

    #region WebSocket DTOs (웹소켓 데이터 전송 객체)

    // ========= JSON 직렬화용 DTO =========

    /// <summary>
    /// 웹소켓 통신을 위한 기본 패킷 구조입니다.
    /// </summary>
    /// <typeparam name="T">데이터 페이로드 타입</typeparam>
    [Serializable]
    public class Packet<T>
    {
        [Tooltip("작업 코드 (Operation Code)")]
        public string op;

        [Tooltip("실제 전송할 데이터")]
        public T data;
    }

    /// <summary>
    /// 채팅 전송용 페이로드입니다.
    /// </summary>
    [Serializable]
    public class ChatPayload
    {
        [Tooltip("채팅 메시지 내용")]
        public string text;

        [Tooltip("감정 분석 요청 여부")]
        public bool emotion;

        [Tooltip("첨부된 이미지 ID 목록")]
        public List<string> image_ids;
        
        // User Info
        [Tooltip("사용자 생년월일")]
        public string user_birth_date;

        [Tooltip("사용자 성별")]
        public string user_gender;

        [Tooltip("사용자 이름")]
        public string user_name;
        
        [Tooltip("사고 모드")]
        public bool think;
        
    }

    /// <summary>
    /// 피드백 전송용 페이로드입니다.
    /// </summary>
    [Serializable]
    public class FeedbackPayload
    {
        [Tooltip("피드백 내용")]
        public string feedback;

        [Tooltip("대상 로그 번호")]
        public int number;
    }
    
    /// <summary>
    /// 클라이언트 핑/퐁 응답용 페이로드입니다.
    /// </summary>
    [Serializable]
    public class ClientPongPayload
    {
        [Tooltip("타임스탬프")]
        public float ts;
    }

    /// <summary>
    /// 데이터가 없는 요청(예: 모니터링 시작)을 위한 빈 페이로드입니다.
    /// </summary>
    [Serializable]
    public class EmptyPayload { }
    
    /// <summary>
    /// 통신상태 점검용 코드입니다.
    /// </summary>
    [Serializable]
    public class HealthResponse
    {
        public string status;
        public string model_name;
    }

    #endregion
}
