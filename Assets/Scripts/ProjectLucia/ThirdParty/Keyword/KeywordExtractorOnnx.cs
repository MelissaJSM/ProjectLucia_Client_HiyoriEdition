using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ProjectLucia.Status;
using UnityEngine;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace ProjectLucia.ThirdParty.Keyword
{
    /// <summary>
    /// ONNX 기반의 키워드 추출기입니다.
    /// 문장을 입력받아 주요 키워드를 추출하고, 임베딩 유사도를 기반으로 순위를 매깁니다.
    /// </summary>
    public class KeywordExtractorOnnx : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Paths (StreamingAssets)")]
        [Tooltip("키워드 추출 모델 파일명 (ONNX)")]
        public string onnxFileName = "keyword_model.onnx";   

        [Tooltip("WordPiece Vocab 파일명")]
        public string vocabFileName = "vocab.txt";   

        [Header("Tokenizer Settings")]
        [Tooltip("최대 시퀀스 길이")]
        public int maxSeqLen = 128;

        [Tooltip("소문자 변환 여부 (Cased 모델이면 false 권장)")]
        public bool lowerCase; 

        [Header("Extraction Settings")]
        [Tooltip("최소 키워드 개수")]
        public int minKeywords = 2;

        [Tooltip("최대 키워드 개수")]
        public int maxKeywords = 6;

        [Tooltip("N-gram 최대 길이")]
        public int ngramMax = 3;

        [Tooltip("후보군 풀 크기")]
        public int topNCandidatePool = 64;

        [Header("Test")]
        [Tooltip("테스트용 입력 문장")]
        [TextArea(2, 4)]
        public string testInput = "사쿠라 미코에 대하여 찾아줄래";

        #endregion

        #region Private Fields (비공개 필드)

        private InferenceSession _session;
        public InferenceSession Session => _session;

        private Dictionary<string, int> _vocab;
        private int _unkId, _clsId, _sepId, _padId;

        // 불용어 및 조사 목록
        private static readonly HashSet<string> Stopwords =
            new HashSet<string>(new[]
            {
                // 기존
                "은","는","이","가","을","를","의","에","에서","에게","과","와","로","으로","한","하고","보다","보다도",
                "에게서","부터","까지","만","라면","라서","라니","랑","던","다가","나면","고",
                "뭐야","알려줘","알려","줘","궁금해","찾아줘","찾아","줘","찾아줄래","찾아볼래",
                "좀","그냥","혹시","약간","매우","너무","제일","가장","같은","듯","듯이","등","또한","그리고","그러나","하지만",
                "정도","경우","대해","대한","대해서","대해서는","대해서도","관련","관련된","관련해","관련하여",
                "대하여","해줘","해","주세요","해주세요","부탁해","부탁해줘","부탁해요","알아봐줄래",

                // 신규(연결/요청/완곡 표현)
                "또","또는","혹은","및","그럼","그러면","그런데",
                "해줘요","해주라","해줄래","해줄래요","해주시겠어요","해주시나요","해주실래요",
                "되나요","되냐","되니","될까","될까요","가능할까요","가능한가요",
                "제발","좀만","정말","진짜"
            });

        // 조사 꼬리 (길이 내림차순)
        private static readonly string[] JosaTails = new[]
        {
            "으로써","으로서","께서는","에게는","까지는","부터는",
            "보다도","로써",
            "에서","에게","께서","이랑",
            "이라면","이라서","이라니","라면","라서","라니","이던","이든","던","든",
            "하고","보다","부터","까지","만","마다","밖에","뿐","조차","마저","겸",
            "으로","라고",
            "은","는","이","가","을","를","의","에","와","과","로","랑","도","나","이나"
        };

        // 문장 끝 어미
        private static readonly string[] SentenceFinals = new[]
        {
            "입니다","입니까","입니까요","습니다","습니까",
            "인가요","인가","나요","예요","이에요","죠","죠요",
            "하세요","해주세요","해줘요","해줄래요","하실래요","할까요","될까요"
        };

        // 토큰 내부 허용 기호
        private const string AllowedInside = "#+_:/@-"; 

        // 정규식 캐시
        private static readonly Regex RxDigit1 = new Regex(@"^\d$", RegexOptions.Compiled);
        private static readonly Regex RxAlpha1 = new Regex(@"^[A-Za-z]$", RegexOptions.Compiled);
        private static readonly Regex RxHasNum = new Regex(@"\d", RegexOptions.Compiled);
        private static readonly Regex RxAcr    = new Regex(@"\b[A-Z]{2,}\b", RegexOptions.Compiled);
        private static readonly Regex RxVer    = new Regex(@"\bv?\d+(\.\d+)+\b", RegexOptions.Compiled);
        private static readonly Regex RxSpaces = new Regex(@"\s+", RegexOptions.Compiled);

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void OnDestroy()
        {
            KeywordModelEnd();
        }

        #endregion

        #region Model Management (모델 관리)

        /// <summary>
        /// 키워드 추출 모델 세션을 시작하고 Vocab을 로드합니다.
        /// </summary>
        public void KeywordModelStart()
        {
            KeywordModelEnd(); // 기존 세션 정리

            var so = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                IntraOpNumThreads = Math.Max(1, SystemInfo.processorCount - 1),
                InterOpNumThreads = 1
            };

            var onnxPath  = Path.Combine(Application.streamingAssetsPath, "Keyword", onnxFileName);
            var vocabPath = Path.Combine(Application.streamingAssetsPath, "Keyword", vocabFileName);

            if (!File.Exists(onnxPath) || !File.Exists(vocabPath))
            {
                if(SettingData.IsDebug) Debug.LogError($"[KeywordExtractorOnnx] 파일 누락: {onnxPath} / {vocabPath}");
                return;
            }

            _session = new InferenceSession(onnxPath, so);

            _vocab = LoadVocab(vocabPath);
            _unkId = GetId("[UNK]");
            _clsId = GetId("[CLS]");
            _sepId = GetId("[SEP]");
            _padId = GetId("[PAD]");

            if(SettingData.IsDebug) Debug.Log($"[KeywordExtractorOnnx] ORT OK, vocab={_vocab.Count}");
        }

        /// <summary>
        /// 키워드 추출 모델 세션을 종료하고 리소스를 해제합니다.
        /// </summary>
        public void KeywordModelEnd()
        {
            try { _session?.Dispose(); } catch { /* no-op */ }
            _session = null;
        }

        [ContextMenu("Test Extract")]
        private void TestExtractMenu()
        {
            if(SettingData.IsDebug) Debug.Log(Extract(testInput));
        }

        #endregion

        #region Public API (공개 메서드)

        /// <summary>
        /// 입력된 텍스트에서 키워드를 추출하여 반환합니다.
        /// </summary>
        /// <param name="text">분석할 텍스트</param>
        /// <returns>추출된 키워드 문자열 (쉼표로 구분)</returns>
        public string Extract(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var normalized = StripSentenceFinal(NormalizeSpaces(text));

            // 토큰화
            var tokens = BasicTokens(normalized, lowerCase);

            // 조사 제거
            for (int i = 0; i < tokens.Count; i++)
                tokens[i] = StripTrailingJosa(tokens[i]);

            // 불용어 제거 및 필터링
            tokens = tokens.Where(KeepToken).ToList();

            // N-gram 후보 생성
            var candidates = GenNgrams(tokens, ngramMax)
                .Select(StripGenericTail)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            candidates = RankCandidatesCheap(candidates, topNCandidatePool);
            if (candidates.Count == 0) return "";

            // 원문 존재 여부 확인
            var present = candidates
                .Where(c => normalized.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (present.Count == 0) return "";

            // 임베딩 및 유사도 계산
            var docEmb = EmbedSentences(new[] { normalized })[0];
            var candEmb = EmbedSentences(present);

            var scored = new List<(string, float)>(present.Count);
            for (int i = 0; i < present.Count; i++)
            {
                float sim = Cosine(docEmb, candEmb[i]);
                scored.Add((present[i], sim));
            }

            if (scored.Count == 0) return "";

            // 점수 기반 정렬 및 중복 제거
            var ranked = scored
                .OrderByDescending(s => BoostScore(s.Item1, s.Item2))
                .Select(s => s.Item1)
                .ToList();

            var deduped = DedupContainment(ranked);

            int take = Math.Min(maxKeywords, Math.Max(1, Math.Min(deduped.Count, maxKeywords)));
            if (deduped.Count < minKeywords) take = Math.Min(maxKeywords, Math.Max(1, deduped.Count));

            return string.Join(", ",
                deduped.Take(take)
                    .Select(s => s.Replace("\"", "").Trim())
                    .Where(s => s.Length > 0));
        }

        #endregion

        #region Embedding Logic (임베딩 로직)

        private float[][] EmbedSentences(IReadOnlyList<string> texts)
        {
            int n = texts.Count;
            if (n == 0) return Array.Empty<float[]>();

            var (ids, mask, types) = TokenizeForBertBatch(texts, maxSeqLen, lowerCase); 
            var shape = new[] { n, maxSeqLen };

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids",      new DenseTensor<long>(ids,  shape)),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, shape)),
            };
            if (_session.InputMetadata.ContainsKey("token_type_ids"))
                inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(types, shape)));

            using var results = _session.Run(inputs);

            var emb = TryGetSentenceEmbeddingBatch(results, n);
            if (emb != null)
                return L2NormalizeBatch(emb);

            var lhs = results.FirstOrDefault(r =>
            {
                var argName = r.Name ?? string.Empty;
                return argName.Contains("last_hidden_state", StringComparison.OrdinalIgnoreCase);
            });

            if (lhs == null)
                throw new Exception("ONNX outputs not recognized (expect sentence_embedding or last_hidden_state).");

            var tensor = lhs.AsTensor<float>();
            var dims = tensor.Dimensions.ToArray(); 
            if (dims.Length != 3 || dims[0] != n)
                throw new Exception($"Unexpected last_hidden_state shape: {string.Join(",", dims)}");

            int batchSize = dims[0], seqLen = dims[1], hiddenSize = dims[2];
            var data = tensor.ToArray();

            var outEmb = new float[batchSize][];
            for (int i = 0; i < batchSize; i++)
            {
                var sum = new float[hiddenSize];
                int valid = 0;
                int rowBase = i * maxSeqLen;

                for (int t = 0; t < seqLen && t < maxSeqLen; t++)
                {
                    if (mask[rowBase + t] == 0) continue;
                    int off = (i * seqLen + t) * hiddenSize;
                    for (int h = 0; h < hiddenSize; h++) sum[h] += data[off + h];
                    valid++;
                }
                if (valid == 0) valid = 1;
                for (int h = 0; h < hiddenSize; h++) sum[h] /= valid;
                outEmb[i] = sum;
            }

            return L2NormalizeBatch(outEmb);
        }

        private static float[][] TryGetSentenceEmbeddingBatch(
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int n)
        {
            foreach (var r in results)
            {
                if (r.Value is not Tensor<float>) continue;

                var t = r.AsTensor<float>();
                var dims = t.Dimensions.ToArray();

                if (dims.Length == 2 && dims[0] == n)
                {
                    int hiddenSize = dims[1];
                    var arr = t.ToArray();
                    var outEmb = new float[n][];
                    for (int i = 0; i < n; i++)
                    {
                        outEmb[i] = new float[hiddenSize];
                        Array.Copy(arr, i * hiddenSize, outEmb[i], 0, hiddenSize);
                    }
                    return outEmb;
                }

                var name = (r.Name ?? string.Empty).ToLowerInvariant();
                if (name.Contains("sentence") && dims.Length >= 2 && dims[0] == n)
                {
                    int hiddenSize = dims[^1];
                    var arr = t.ToArray();
                    var outEmb = new float[n][];
                    for (int i = 0; i < n; i++)
                    {
                        outEmb[i] = new float[hiddenSize];
                        Array.Copy(arr, i * hiddenSize, outEmb[i], 0, hiddenSize);
                    }
                    return outEmb;
                }
            }
            return null;
        }

        private static float[][] L2NormalizeBatch(float[][] emb)
        {
            foreach (var t in emb)
            {
                double s = 0;
                foreach (var t1 in t)
                    s += t1 * t1;

                float inv = (float)(1.0 / Math.Sqrt(s + 1e-12));
                for (int h = 0; h < t.Length; h++) t[h] *= inv;
            }

            return emb;
        }

        #endregion

        #region Tokenizer Logic (토크나이저 로직)

        private (long[] ids, long[] mask, long[] types) TokenizeForBertBatch(IReadOnlyList<string> texts, int maxLen, bool lower)
        {
            int n = texts.Count;
            var ids   = new long[n * maxLen];
            var mask  = new long[n * maxLen];
            var types = new long[n * maxLen]; 

            for (int i = 0; i < n; i++)
            {
                var basic = BasicTokens(texts[i], lower);
                var wp = new List<int>(basic.Count + 2) { _clsId };
                foreach (var tok in basic)
                {
                    foreach (var id in WordPiece(tok))
                        wp.Add(id);
                }
                wp.Add(_sepId);

                if (wp.Count > maxLen) wp = wp.Take(maxLen).ToList();

                int baseIdx = i * maxLen;
                for (int t = 0; t < wp.Count; t++)
                {
                    ids[baseIdx + t] = wp[t];
                    mask[baseIdx + t] = 1;
                }
                for (int t = wp.Count; t < maxLen; t++)
                {
                    ids[baseIdx + t] = _padId;
                    mask[baseIdx + t] = 0;
                }
            }
            return (ids, mask, types);
        }

        private List<string> BasicTokens(string text, bool lower)
        {
            var norm = text.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(norm.Length);

            for (int i = 0; i < norm.Length; i++)
            {
                char ch = norm[i];
                if (char.IsWhiteSpace(ch)) { sb.Append(' '); continue; }

                var cat = char.GetUnicodeCategory(ch);
                bool isLetterOrDigit = char.IsLetterOrDigit(ch) || cat == UnicodeCategory.OtherLetter;
                if (isLetterOrDigit)
                {
                    sb.Append(lower ? char.ToLowerInvariant(ch) : ch);
                    continue;
                }

                if (ch == '.' && i > 0 && i < norm.Length - 1 &&
                    IsAlphaNum(norm[i - 1]) && IsAlphaNum(norm[i + 1]))
                {
                    sb.Append('.');
                    continue;
                }

                if (AllowedInside.IndexOf(ch) >= 0 &&
                    i > 0 && i < norm.Length - 1 &&
                    IsAlphaNum(norm[i - 1]) && IsAlphaNum(norm[i + 1]))
                {
                    sb.Append(ch);
                    continue;
                }

                sb.Append(' ');
            }

            return sb.ToString()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            static bool IsAlphaNum(char c) =>
                char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) == UnicodeCategory.OtherLetter;
        }

        private IEnumerable<int> WordPiece(string token, int maxCharsPerWord = 100)
        {
            if (token.Length > maxCharsPerWord) return new[] { _unkId };

            var chars = token.ToCharArray();
            var start = 0;
            var subTokens = new List<int>();
            while (start < chars.Length)
            {
                int end = chars.Length;
                int curId = -1;
                string substr = null;

                while (start < end)
                {
                    var piece = new string(chars, start, end - start);
                    if (start > 0) piece = "##" + piece;
                    if (_vocab.TryGetValue(piece, out var vid))
                    {
                        curId = vid; substr = piece; break;
                    }
                    end -= 1;
                }
                if (curId == -1 || substr == null) { subTokens.Add(_unkId); break; }

                subTokens.Add(curId);
                start = substr.StartsWith("##", StringComparison.Ordinal) ? start + substr.Length - 2 : start + substr.Length;
            }
            return subTokens;
        }

        private Dictionary<string, int> LoadVocab(string path)
        {
            var dict = new Dictionary<string, int>(50000);
            foreach (var line in File.ReadAllLines(path))
            {
                var tok = line.Trim();
                if (!dict.ContainsKey(tok)) dict.Add(tok, dict.Count);
            }
            return dict;
        }

        private int GetId(string token) => _vocab.GetValueOrDefault(token, 0);

        #endregion

        #region Helper Methods (보조 메서드)

        private static string NormalizeSpaces(string s) =>
            RxSpaces.Replace(s, " ").Trim();

        private static string StripSentenceFinal(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (var tail in SentenceFinals)
            {
                if (text.EndsWith(tail, StringComparison.Ordinal))
                    return text[..^tail.Length];
            }
            if (text.EndsWith("요", StringComparison.Ordinal) && text.Length > 1)
                return text[..^1];

            return text;
        }

        private static string StripTrailingJosa(string token)
        {
            foreach (var j in JosaTails) 
            {
                if (token.EndsWith(j, StringComparison.Ordinal) && token.Length > j.Length)
                    return token.Substring(0, token.Length - j.Length);
            }
            return token;
        }

        private static string StripGenericTail(string phrase)
        {
            var parts = phrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return phrase;
            var tail = parts[^1];
            if (Stopwords.Contains(tail)) return string.Join(" ", parts.Take(parts.Length - 1));
            return phrase;
        }

        private static List<string> GenNgrams(List<string> toks, int nmax)
        {
            var cands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int n = 1; n <= nmax; n++)
            {
                for (int i = 0; i <= toks.Count - n; i++)
                {
                    var spanTokens = toks.Skip(i).Take(n).ToList();
                    if (spanTokens.Any(t => Stopwords.Contains(t))) continue;

                    var span = string.Join(" ", spanTokens).Trim();
                    if (span.Length >= 2 && !Stopwords.Contains(span))
                        cands.Add(span);
                }
            }
            return cands.ToList();
        }

        private static bool KeepToken(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return false;
            if (Stopwords.Contains(t)) return false;

            if (t.Length < 2)
            {
                if (RxDigit1.IsMatch(t)) return true;        
                if (RxAlpha1.IsMatch(t)) return true;        
                return false;
            }
            return true;
        }

        private static List<string> RankCandidatesCheap(List<string> cands, int limit)
        {
            float Score(string s)
            {
                int len = s.Length;
                bool hasDigit = s.Any(char.IsDigit);
                bool hasUpper = s.Any(char.IsUpper);
                int words = s.Count(ch => ch == ' ') + 1;
                return len + (hasDigit ? 2f : 0f) + (hasUpper ? 1f : 0f) + words * 0.5f;
            }
            return cands.OrderByDescending(Score).Take(Math.Max(8, limit)).ToList();
        }

        private static float Cosine(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            int n = Math.Min(a.Count, b.Count);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < n; i++) { double x = a[i], y = b[i]; dot += x * y; na += x * x; nb += y * y; }
            if (na == 0 || nb == 0) return 0f;
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-12));
        }

        private static double BoostScore(string phrase, float sim)
        {
            bool hasNum = RxHasNum.IsMatch(phrase);
            bool hasAcr = RxAcr.IsMatch(phrase);
            bool hasVer = RxVer.IsMatch(phrase);
            double boost = sim;
            if (hasNum) boost += 0.04;
            if (hasAcr) boost += 0.05;
            if (hasVer) boost += 0.03;
            boost += Math.Min(phrase.Length, 30) * 0.001; 
            return boost;
        }

        private static List<string> DedupContainment(List<string> ranked)
        {
            var kept = new List<string>();
            foreach (var c in ranked)
            {
                string cc = c.Trim();
                if (cc.Length == 0) continue;

                bool drop = false;
                for (int i = kept.Count - 1; i >= 0; i--)
                {
                    var k = kept[i];
                    if (cc.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !cc.Equals(k, StringComparison.OrdinalIgnoreCase))
                    { drop = true; break; }

                    if (k.IndexOf(cc, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !cc.Equals(k, StringComparison.OrdinalIgnoreCase))
                    { kept.RemoveAt(i); }
                }
                if (!drop) kept.Add(cc);
            }
            return kept;
        }

        #endregion
    }
}
