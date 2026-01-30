using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using ProjectLucia.GUI;
using UnityEngine;
using ProjectLucia.Status;

namespace ProjectLucia.Server
{
    /// <summary>
    /// MySQL 데이터베이스와의 연결 및 CRUD 작업을 관리하는 클래스입니다.
    /// Unity 메인 스레드 의존성을 최소화하고, 비동기(Async) 메서드를 지원하여 성능을 최적화했습니다.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class MySQLManager : MonoBehaviour
    {
        #region Private Fields (비공개 필드)

        // 선택적 Unity 의존성 (백그라운드 스레드 사용 시 주의)
        private ActionManager _actionManager;
        private ServerClient _serverClient;

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// true일 경우 Unity API 호출(Debug.Log, ActionManager 등)을 억제합니다.
        /// 백그라운드 스레드에서 DB 작업을 수행할 때 true로 설정해야 합니다.
        /// </summary>
        public bool SuppressUnitySideEffects { get; set; }

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            try
            {
                _actionManager = GameManager.Instance.ActionManager;
                _serverClient = GameManager.Instance.ServerClient;
            }
            catch (Exception e)
            {
                // 메인 스레드 전용 API 사용 금지. 가벼운 로그만 출력.
                Debug.Log($"[MySQLManager.Awake] 참고: {e.Message}");
            }
        }

        #endregion

        #region Connection Management (연결 관리)

        /// <summary>
        /// 현재 설정된 정보로 연결 문자열을 생성합니다.
        /// </summary>
        private string BuildConnectionString()
        {
            return $"Server={SettingData.MySqlIp};Database={SettingData.DatabaseName};User ID={SettingData.SqlUserName};Password={SettingData.SqlPassword};Port={SettingData.SqlPort};SslMode=None;";
        }

        /// <summary>
        /// 새로운 MySQL 연결 객체를 생성합니다.
        /// </summary>
        private MySqlConnection CreateConnection() => new MySqlConnection(BuildConnectionString());

        /// <summary>
        /// 데이터베이스 연결을 테스트합니다.
        /// </summary>
        /// <param name="open">true면 연결을 시도하고 결과를 로그로 출력합니다.</param>
        public void ConnectToDatabase(bool open)
        {
            try
            {
                if (open)
                {
                    using var conn = CreateConnection();
                    conn.Open();
                    if (!SuppressUnitySideEffects)
                    {
                        Debug.Log("MySQL 연결 성공!");
                        SettingData.IsIntroSql = true;
                    }
                }
                else
                {
                    if (!SuppressUnitySideEffects)
                        Debug.Log("MySQL 연결 종료 요청(지역 커넥션 전략에서는 실동작 없음)");
                }
            }
            catch (Exception ex)
            {
                if (!SuppressUnitySideEffects)
                {
                    Debug.LogError("MySQL 연결 실패: " + ex.Message);
                    SettingData.IsIntroSql = false;
                    try
                    {
                        _actionManager?.ErrorCharacterAction(1000, false);
                    }
                    catch (Exception innerEx)
                    {
                        Debug.LogWarning($"[MySQLManager] ActionManager Error: {innerEx.Message}");
                    }
                    return;
                }
                throw;
            }
        }

        /// <summary>
        /// MySQL 서버 재연결을 비동기로 시도합니다.
        /// </summary>
        public async Task OnRestartSQLServerAsync(CancellationToken ct = default)
        {
            await using (var conn = CreateConnection())
            {
                await conn.OpenAsync(ct);
            }

            if (!SuppressUnitySideEffects)
                Debug.Log("MySQL 재연결 성공");
        }

        #endregion

        #region CRUD Operations (데이터 조작)

        // -------------------- INSERT --------------------

        /// <summary>
        /// 대화 로그를 데이터베이스에 저장합니다. (동기)
        /// </summary>
        public void InsertLogData(string userText, string assistantText, string emotion)
        {
            int rows = 0;
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO logs
(user, assistant, emotion, isFeedback, isLearning, userTime, assistantTime, feedbackData, feedbackHint)
VALUES (@user, @assistant, @emotion, @isFeedback, @isLearning, @userTime, @assistantTime, @feedbackData, @feedbackHint);";

                    cmd.Parameters.Add("@user", MySqlDbType.VarChar, 4000).Value = userText ?? string.Empty;
                    cmd.Parameters.Add("@assistant", MySqlDbType.VarChar, 4000).Value = assistantText ?? string.Empty;
                    cmd.Parameters.Add("@emotion", MySqlDbType.VarChar, 64).Value = emotion ?? string.Empty;
                    cmd.Parameters.Add("@isFeedback", MySqlDbType.Bit).Value = false;
                    cmd.Parameters.Add("@isLearning", MySqlDbType.Bit).Value = false;
                    var userTime = _serverClient != null && DateTime.TryParse(_serverClient.userDateTime, out var ut) ? ut : DateTime.Now;
                    cmd.Parameters.Add("@userTime", MySqlDbType.DateTime).Value = userTime;
                    cmd.Parameters.Add("@assistantTime", MySqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@feedbackData", MySqlDbType.VarChar, 4000).Value = string.Empty;
                    cmd.Parameters.Add("@feedbackHint", MySqlDbType.VarChar, 4000).Value = string.Empty;

                    cmd.CommandTimeout = 5;
                    rows = cmd.ExecuteNonQuery();
                }
            }

            if (!SuppressUnitySideEffects)
            {
                if (rows > 0) Debug.Log("데이터 삽입 성공!");
                else Debug.LogWarning("데이터 삽입 실패 (0 rows affected)");
            }

            if (_serverClient != null) _serverClient.userDateTime = null;
        }

        /// <summary>
        /// 대화 로그를 데이터베이스에 저장합니다. (비동기)
        /// </summary>
        public async Task InsertLogDataAsync(string userText, string assistantText, string emotion, CancellationToken ct = default)
        {
            int rows = 0;
            await using (var conn = CreateConnection())
            {
                await conn.OpenAsync(ct);
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO logs
(user, assistant, emotion, isFeedback, isLearning, userTime, assistantTime, feedbackData, feedbackHint)
VALUES (@user, @assistant, @emotion, @isFeedback, @isLearning, @userTime, @assistantTime, @feedbackData, @feedbackHint);";

                    cmd.Parameters.Add("@user", MySqlDbType.VarChar, 4000).Value = userText ?? string.Empty;
                    cmd.Parameters.Add("@assistant", MySqlDbType.VarChar, 4000).Value = assistantText ?? string.Empty;
                    cmd.Parameters.Add("@emotion", MySqlDbType.VarChar, 64).Value = emotion ?? string.Empty;
                    cmd.Parameters.Add("@isFeedback", MySqlDbType.Bit).Value = false;
                    cmd.Parameters.Add("@isLearning", MySqlDbType.Bit).Value = false;
                    var userTime = _serverClient != null && DateTime.TryParse(_serverClient.userDateTime, out var ut) ? ut : DateTime.Now;
                    cmd.Parameters.Add("@userTime", MySqlDbType.DateTime).Value = userTime;
                    cmd.Parameters.Add("@assistantTime", MySqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@feedbackData", MySqlDbType.VarChar, 4000).Value = string.Empty;
                    cmd.Parameters.Add("@feedbackHint", MySqlDbType.VarChar, 4000).Value = string.Empty;
                    cmd.CommandTimeout = 5;

                    rows = await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            if (!SuppressUnitySideEffects)
            {
                if (rows > 0) Debug.Log("데이터 삽입 성공!");
                else Debug.LogWarning("데이터 삽입 실패 (0 rows affected)");
            }

            if (_serverClient != null) _serverClient.userDateTime = null;
        }

        // -------------------- SELECT --------------------

        /// <summary>
        /// 저장된 로그 데이터를 조회합니다. (동기)
        /// </summary>
        /// <param name="limit">조회할 개수 (0이면 전체)</param>
        /// <param name="offset">건너뛸 개수</param>
        public List<LogData> InQuiryLogData(int limit, int offset)
        {
            var logDataList = new List<LogData>();
            using (var conn = CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = (limit == 0)
                        ? @"SELECT id, user, assistant, emotion, userTime, assistantTime, isFeedback, isLearning, feedbackData FROM logs ORDER BY userTime DESC;"
                        : @"SELECT id, user, assistant, emotion, userTime, assistantTime, isFeedback, isLearning, feedbackData FROM logs ORDER BY userTime DESC LIMIT @limit OFFSET @offset;";

                    if (limit != 0)
                    {
                        cmd.Parameters.Add("@limit", MySqlDbType.Int32).Value = limit;
                        cmd.Parameters.Add("@offset", MySqlDbType.Int32).Value = offset;
                    }

                    cmd.CommandTimeout = 5;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            logDataList.Add(new LogData
                            {
                                id = reader.GetInt32("id"),
                                user = reader.GetString("user"),
                                assistant = reader.GetString("assistant"),
                                emotion = reader.GetString("emotion"),
                                userTime = reader.GetDateTime("userTime").ToString("yyyy-MM-dd HH:mm:ss"),
                                assistantTime = reader.GetDateTime("assistantTime").ToString("yyyy-MM-dd HH:mm:ss"),
                                isFeedback = !reader.IsDBNull(reader.GetOrdinal("isFeedback")) && reader.GetBoolean(reader.GetOrdinal("isFeedback")),
                                isLearning = !reader.IsDBNull(reader.GetOrdinal("isLearning")) && reader.GetBoolean(reader.GetOrdinal("isLearning")),
                                feedbackData = reader.IsDBNull(reader.GetOrdinal("feedbackData")) ? string.Empty : reader.GetString(reader.GetOrdinal("feedbackData")),
                            });
                        }
                    }
                }
            }

            if (!SuppressUnitySideEffects)
                Debug.Log("데이터 조회 성공!");

            return logDataList;
        }

        /// <summary>
        /// 저장된 로그 데이터를 조회합니다. (비동기)
        /// </summary>
        public async Task<List<LogData>> InQuiryLogDataAsync(int limit, int offset, CancellationToken ct = default)
        {
            var list = new List<LogData>();
            await using (var conn = CreateConnection())
            {
                await conn.OpenAsync(ct);
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = (limit == 0)
                        ? @"SELECT id, user, assistant, emotion, userTime, assistantTime, isFeedback, isLearning, feedbackData FROM logs ORDER BY userTime DESC;"
                        : @"SELECT id, user, assistant, emotion, userTime, assistantTime, isFeedback, isLearning, feedbackData FROM logs ORDER BY userTime DESC LIMIT @limit OFFSET @offset;";

                    if (limit != 0)
                    {
                        cmd.Parameters.Add("@limit", MySqlDbType.Int32).Value = limit;
                        cmd.Parameters.Add("@offset", MySqlDbType.Int32).Value = offset;
                    }

                    cmd.CommandTimeout = 5;

                    await using (var reader = await cmd.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            list.Add(new LogData
                            {
                                id = reader.GetInt32("id"),
                                user = reader.GetString("user"),
                                assistant = reader.GetString("assistant"),
                                emotion = reader.GetString("emotion"),
                                userTime = reader.GetDateTime("userTime").ToString("yyyy-MM-dd HH:mm:ss"),
                                assistantTime = reader.GetDateTime("assistantTime").ToString("yyyy-MM-dd HH:mm:ss"),
                                isFeedback = !reader.IsDBNull(reader.GetOrdinal("isFeedback")) && reader.GetBoolean(reader.GetOrdinal("isFeedback")),
                                isLearning = !reader.IsDBNull(reader.GetOrdinal("isLearning")) && reader.GetBoolean(reader.GetOrdinal("isLearning")),
                                feedbackData = reader.IsDBNull(reader.GetOrdinal("feedbackData")) ? string.Empty : reader.GetString(reader.GetOrdinal("feedbackData")),
                            });
                        }
                    }
                }
            }

            if (!SuppressUnitySideEffects)
                Debug.Log("데이터 조회 성공!");

            return list;
        }

        // -------------------- UPDATE --------------------

        /// <summary>
        /// 피드백 데이터를 업데이트합니다. (동기)
        /// </summary>
        public void UpdateFeedbackData(string feedbackData, string feedbackHint, int id)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE logs SET feedbackData = @feedbackData, feedbackHint = @feedbackHint, isFeedback = @isFeedback WHERE id = @id;";
            cmd.Parameters.Add("@feedbackData", MySqlDbType.VarChar, 4000).Value = feedbackData ?? string.Empty;
            cmd.Parameters.Add("@feedbackHint", MySqlDbType.VarChar, 4000).Value = feedbackHint ?? string.Empty;
            cmd.Parameters.Add("@isFeedback", MySqlDbType.Bit).Value = true;
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
            cmd.CommandTimeout = 5;
            int rows = cmd.ExecuteNonQuery();

            if (!SuppressUnitySideEffects)
            {
                if (rows > 0) _actionManager?.SuccessCharacterAction(10); else _actionManager?.ErrorCharacterAction(1003, false);
            }
        }

        /// <summary>
        /// 피드백 데이터를 업데이트합니다. (비동기)
        /// </summary>
        public async Task UpdateFeedbackDataAsync(string feedbackData, string feedbackHint, int id, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE logs SET feedbackData = @feedbackData, feedbackHint = @feedbackHint, isFeedback = @isFeedback WHERE id = @id;";
            cmd.Parameters.Add("@feedbackData", MySqlDbType.VarChar, 4000).Value = feedbackData ?? string.Empty;
            cmd.Parameters.Add("@feedbackHint", MySqlDbType.VarChar, 4000).Value = feedbackHint ?? string.Empty;
            cmd.Parameters.Add("@isFeedback", MySqlDbType.Bit).Value = true;
            cmd.Parameters.Add("@id", MySqlDbType.Int32).Value = id;
            cmd.CommandTimeout = 5;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        // -------------------- DELETE --------------------

        /// <summary>
        /// 특정 로그 데이터를 삭제합니다. (동기)
        /// </summary>
        public bool DeleteLogData(int logId)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM logs WHERE id = @logId;";
            cmd.Parameters.Add("@logId", MySqlDbType.Int32).Value = logId;
            cmd.CommandTimeout = 5;
            int rows = cmd.ExecuteNonQuery();
            return rows > 0;
        }

        /// <summary>
        /// 특정 로그 데이터를 삭제합니다. (비동기)
        /// </summary>
        public async Task<bool> DeleteLogDataAsync(int logId, CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM logs WHERE id = @logId;";
            cmd.Parameters.Add("@logId", MySqlDbType.Int32).Value = logId;
            cmd.CommandTimeout = 5;
            int rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        /// <summary>
        /// 모든 로그 데이터를 삭제합니다. (동기)
        /// </summary>
        public bool AllDeleteLogData()
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM logs;";
            cmd.CommandTimeout = 5;
            int rows = cmd.ExecuteNonQuery();
            if (!SuppressUnitySideEffects)
                Debug.Log($"총 {rows}개의 로그 삭제됨.");
            return true;
        }

        /// <summary>
        /// 모든 로그 데이터를 삭제합니다. (비동기)
        /// </summary>
        public async Task<bool> AllDeleteLogDataAsync(CancellationToken ct = default)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM logs;";
            cmd.CommandTimeout = 5;
            int rows = await cmd.ExecuteNonQueryAsync(ct);
            if (!SuppressUnitySideEffects)
                Debug.Log($"총 {rows}개의 로그 삭제됨.");
            return true;
        }

        // -------------------- Server Settings (Deprecated) --------------------

        /// <summary>
        /// 서버 설정 데이터를 조회합니다. (현재 사용되지 않음)
        /// </summary>
        public ServerSettingData InQuiryServerSettingData()
        {
            // 실제 DB 연결 테스트를 수행하여 연결 가능 여부를 확인합니다.
            try
            {
                using var conn = CreateConnection();
                conn.Open();
                // 연결 성공 시 빈 객체 반환 (성공 의미)
                return new ServerSettingData();
            }
            catch
            {
                // 연결 실패 시 null 반환
                return null;
            }
        }

        public Task<ServerSettingData> InQuiryServerSettingDataAsync(CancellationToken ct = default)
        {
            // memoryLimit, memoryTime 제거됨
            return Task.FromResult(new ServerSettingData());
        }

        /// <summary>
        /// 서버 설정 데이터를 업데이트합니다. (현재 사용되지 않음)
        /// </summary>
        public void ReplaceServerSettingData()
        {
            // memoryLimit, memoryTime 제거됨
            if (!SuppressUnitySideEffects)
                Debug.Log("설정값 업데이트 성공! (No-op)");
        }

#pragma warning disable CS1998
        public async Task ReplaceServerSettingDataAsync(CancellationToken ct = default)
        {
            // memoryLimit, memoryTime 제거됨
             if (!SuppressUnitySideEffects)
                Debug.Log("설정값 업데이트 성공! (No-op)");
             await Task.CompletedTask;
        }
#pragma warning restore CS1998

        #endregion
    }
}
