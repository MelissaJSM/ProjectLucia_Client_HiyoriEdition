using System.Collections;
using System.Diagnostics;
using ProjectLucia.Status; // 프로세스 감지에 필요
using UnityEngine;
using Debug = UnityEngine.Debug;



namespace ProjectLucia.Windows
{
    /// <summary>
    /// 미구현
    /// </summary>
    public class ProgramMonitor : MonoBehaviour
    {
        [SerializeField] private string targetProgram = "DNF"; // 감지할 프로그램 이름 (확장자 제외)

        [SerializeField] private float checkInterval = 1.0f; // 프로그램 상태를 확인하는 주기 (초)

        private bool _isProgramRunning; // 프로그램 실행 상태를 추적
        
        private Coroutine _programMonitorCoroutine;

        void Start()
        {
            // 초기 상태 확인
            _isProgramRunning = IsProgramRunning();

            // 초기 실행 중인 상태에 따라 로그 출력 (선택 사항)
            if (_isProgramRunning)
            {
                if(SettingData.IsDebug) Debug.Log($"{targetProgram}이(가) 이미 실행 중입니다.");
            }

            // 코루틴 시작
            _programMonitorCoroutine = StartCoroutine(CheckProgramStatusCoroutine());
        }

        // 프로그램 상태를 주기적으로 확인하는 코루틴
        private IEnumerator CheckProgramStatusCoroutine()
        {
            while (true)
            {
                // 현재 프로그램 실행 상태 확인
                bool isRunningNow = IsProgramRunning();

                // 실행 상태가 변경되었을 때만 로그 출력
                if (isRunningNow && !_isProgramRunning)
                {
                    if(SettingData.IsDebug) Debug.Log($"{targetProgram}이(가) 실행되었습니다.");
                }
                else if (!isRunningNow && _isProgramRunning)
                {
                    if(SettingData.IsDebug) Debug.Log($"{targetProgram}이(가) 종료되었습니다.");
                }

                // 현재 상태를 저장
                _isProgramRunning = isRunningNow;

                // 지정된 간격만큼 대기
                yield return new WaitForSeconds(checkInterval);
            }
            // ReSharper disable once IteratorNeverReturns
        }

        // 프로그램 실행 여부를 확인하는 함수
        private bool IsProgramRunning()
        {
            Process[] processes = Process.GetProcessesByName(targetProgram);
            return processes.Length > 0;
        }
        
        
        private void OnDestroy()
        {
            if (_programMonitorCoroutine != null)
            {
                StopCoroutine(_programMonitorCoroutine);
                _programMonitorCoroutine = null;
            }
            Resources.UnloadUnusedAssets();
        }
    }
}