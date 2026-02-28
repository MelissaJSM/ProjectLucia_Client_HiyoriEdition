# Project Lucia (프로젝트 루시아)

**Project Lucia**는 Live2D 캐릭터와 실시간으로 소통할 수 있는 **인터랙티브 AI 데스크톱 어시스턴트**입니다.  
사용자의 목소리를 인식하고, 화자를 검증하며, 감정에 따라 반응하는 Live2D 캐릭터를 통해 몰입감 있는 대화 경험을 제공합니다.

---

## 📖 목차
1. [소개](#-소개)
2. [주요 기능](#-주요-기능)
3. [기술 스택](#-기술-스택)
4. [설치 방법](#-설치-방법)
5. [사용법](#-사용법)

---

## 📝 소개
이 프로젝트는 Unity 엔진을 기반으로 개발되었으며, 단순한 챗봇을 넘어 시각적(Live2D) 및 청각적(Voice) 상호작용을 강화한 AI 비서입니다.  
사용자의 목소리를 듣고(STT), 누구인지 확인(ASV)하며, 대화 내용을 데이터베이스(MySQL)에 저장하여 기억합니다. 또한, RAG(검색 증강 생성) 기술을 통해 문맥에 맞는 답변을 제공할 수 있도록 설계되었습니다.

---

## ✨ 주요 기능

### 1. Live2D 캐릭터 상호작용
- **감정 표현**: 대화 내용에 따라 기쁨, 슬픔, 화남 등 다양한 표정과 모션을 보여줍니다.
- **터치 및 드래그**: 캐릭터의 머리나 몸을 터치하면 반응하며, 마우스로 드래그하여 화면 내 위치를 자유롭게 이동시킬 수 있습니다.
- **동적 UI 배치**: 캐릭터의 크기나 위치에 따라 말풍선 및 UI가 자동으로 조정됩니다.

### 2. 고급 음성 인식 및 화자 인증
- **Whisper STT**: OpenAI의 Whisper 모델을 사용하여 높은 정확도의 음성 인식을 지원합니다.
- **Silero VAD**: 목소리가 들릴 때만 녹음을 수행하여 효율성을 높입니다.
- **화자 인증 (Speaker Verification)**: ONNX Runtime과 VoxCeleb 모델을 사용하여 등록된 사용자(주인님)의 목소리인지 검증합니다. 타인의 목소리는 무시하거나 차단할 수 있습니다.

### 3. 데이터 관리 및 기억
- **MySQL 연동**: 모든 대화 로그를 데이터베이스에 저장하고 조회할 수 있습니다.
- **피드백 시스템**: 대화 로그에서 AI의 답변을 수정하거나 피드백을 보내 학습 데이터를 개선할 수 있습니다.

### 4. 지능형 대화 (RAG)
- **키워드 추출**: 사용자 입력으로 관련 정보를 검색하거나 RAG 서버로 전송합니다.

---

## 🛠 기술 스택

| 분류 | 기술 | 비고 |
| :--- | :--- | :--- |
| **Engine** | **Unity** | 6000.3.5 (C#) |
| **Visual** | **Live2D Cubism SDK** | 캐릭터 애니메이션 및 상호작용 |
| **AI / ML** | **OpenAI Whisper** | 음성 인식 (STT) |
| | **ONNX Runtime** | 화자 인증(VoxCeleb), VAD(Silero), 키워드 추출 |
| **Database** | **MySQL** | 대화 로그 및 설정 저장 (MySqlConnector) |
| **Network** | **UnityWebRequest** | REST API 통신 |

---

## 📦 설치 방법

### 1. 사전 요구 사항
- **Unity Editor**: 프로젝트 버전에 맞는 Unity 에디터 설치. - 현재 프로젝트
- [(**MySQL Server**)](https://github.com/MelissaJSM/ProjectLucia_Server_HiyoriEdition.git): 로컬 또는 원격 서버에 MySQL 데이터베이스가 설치되어 있어야 합니다.
- [(**Backend Server**)](https://github.com/MelissaJSM/ProjectLucia_Server_HiyoriEdition.git): 이 클라이언트와 통신할 AI 추론 서버(Python/FastAPI 등)가 필요합니다.

### 2. 프로젝트 설정
1. 저장소를 클론합니다.
   ```bash
   git clone https://github.com/MelissaJSM/ProjectLucia_Client_HiyoriEdition.git
   ```
2. Unity Hub에 프로젝트를 추가하고 엽니다.
3. `Fonts` 폴더 내에 압축파일을 현재 폴더에 해제합니다.
4. [(유니티 sdk)](https://www.live2d.com/en/sdk/download/unity/) 유니티 sdk 를 다운받아서 프로젝트에 설치합니다.

### 3. 데이터베이스 설정
- MySQL에 접속하여 프로젝트에서 사용할 데이터베이스와 테이블(`logs` 등)을 생성합니다. (파일은 서버쪽에 있음)

---

## 🎮 사용법

### 초기 설정 (Settings)
1. 앱을 실행하고 **설정(Settings)** 메뉴로 진입합니다.
2. **서버 설정**: AI 백엔드 서버의 IP와 포트를 입력합니다.
3. **DB 설정**: MySQL IP, 포트, ID, 비밀번호, DB 이름을 입력합니다.
4. **마이크 설정**: 사용할 마이크 장치를 선택합니다.

### 대화하기
- **음성 대화**: 마이크에 대고 말을 걸면 VAD가 목소리를 감지하고, 화자 인증을 거쳐 답변을 생성합니다.
- **텍스트 대화**: 하단 입력창을 통해 텍스트로 대화할 수 있습니다.

### 로그 및 피드백
- **로그 확인**: 우측 메뉴에서 로그 버튼을 눌러 지난 대화 내역을 확인합니다.
- **피드백**: AI의 답변이 마음에 들지 않으면 로그를 우클릭하여 수정된 답변(피드백)을 전송할 수 있습니다.

### 캐릭터 조작
- **이동**: 캐릭터를 마우스 왼쪽 버튼으로 드래그하여 원하는 위치로 옮깁니다.
- **터치**: 캐릭터를 클릭하면 랜덤한 모션이나 표정 반응을 보입니다.

### 캐릭터 교체
- **Params**: 캐릭터 교체 작업을 할때 live2d 캐릭터의 Params 오브젝트 에 등록된 인스펙터를 반드시 확인하여 어느 부위에 인스펙터가 추가되어있는지 확인하고 교체 부탁드립니다.
    - (+)ParamAngleX
      * Cubism Look Parameter (X / 30)
     - (+)ParamAngleX
       * Cubism Look Parameter (Y / 30)
     - (+)ParamBodyAngleX
       * Cubism Look Parameter (X / 10)
     - (+)ParamBodyAngleY
       * Cubism Look Parameter (Y / 10)
     - (+)ParamBodyAngleZ
       * Cubism Look Parameter (X / 10)
     - (+)ParamBodyAngleX2 (있을경우)
       * Cubism Look Parameter (X / 10)
     - (+)ParamBodyAngleY2 (있을경우)
       * Cubism Look Parameter (Y / 10)
     - (+)ParamBodyAngleZ2 (있을경우)
       * Cubism Look Parameter (X / 10)
     - (+)ParamMouthOpenY
       * Cubism Mouth Parameter
     - (+)ParamEyeBallX
       * Cubism Look Parameter (X / 1)
     - (+)ParamEyeBallX
       * Cubism Look Parameter (Y / 1)
     - (+)ParamBreath
       * Cubism Harmonic Motion Parameter
- **Live2d Prefab**: 캐릭터 교체 작업을 할때 live2d 캐릭터의 Prefab 에 등록된 인스펙터를 반드시 확인하여주시길바랍니다.
      -[] Transform
      -[] Cubism Model(스크립트)
      -[] Cubism Parameters Inspector(스크립트)
      - []Cubism Parts Inspector(스크립트)
      - []Cubism Render Controller(스크립트)
      - []Cubism Eye Blink Controller(스크립트)
      - []Cubism Display Info Combined Parameter(스크립트)
      - []Cubism Mask Controller(스크립트)
      - []Cubism Update Controller(스크립트)
      - []Cubism Parameter Store(스크립트)
      - []Cubism Pose Controller(스크립트)
      - (+)Cubism Expression Controller(스크립트)
        * Express List 에 에셋오브젝트를 반드시 넣어주십시오.
        * 해당 캐릭터에 맞는 에셋 오브젝트를 넣으셔야 합니다.
        * 없으면 직접 만들어야 할 수도 있습니다.
      - []Animator
      - (+)Cubism Fade Controller(스크립트)
        * Cubism Fade Motion 에 에셋오브젝트를 반드시 넣어주십시오.
        * 해당 캐릭터에 맞는 에셋 오브젝트를 넣으셔야 합니다.
        * 없으면 직접 만들어야 할 수도 있습니다.
      - []Cubism Physics Controller(스크립트)
      - (+)Cubism Mouth Controller(스크립트)
      - (+)Cubism Auto Eye Blink Input(스크립트)
      - (+)Cubism Look Controller(스크립트)
      - (+)Cubism Audio Mouth Input(스크립트)
        * 해당 Audio Input에 InputObject/AudioInput 하이라키를 넣어주십시오.
      - (+)Cubism Harmonic Motion Controller(스크립트)
      - (+)Cubism Raycaster(스크립트)
      - (+)Cubism Motion Controller(스크립트)

---
*Developed by MelissaJ*
