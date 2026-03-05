using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using ProjectLucia.Windows;

// ★ 카테고리를 구분하기 위한 열거형 선언
public enum AudioCategory { Positive, Negative } 

public class WavImporter : MonoBehaviour
{
    [Header("Data Lists")]
    public List<string> positivePaths = new List<string>();
    public List<string> negativePaths = new List<string>();

    [Header("UI Reference")]
    public Transform positiveContentPanel; // Positive용 Content 패널
    public Transform negativeContentPanel; // Negative용 Content 패널
    public GameObject itemPrefab;  

    private TransparentApp _transparentApp;

    private void Start()
    {
        // TransparentApp 참조 찾기 (씬에 하나만 있다고 가정)
        _transparentApp = FindObjectOfType<TransparentApp>();

        LoadExistingFiles(AudioCategory.Positive);
        LoadExistingFiles(AudioCategory.Negative);
    }

    private string GetFolderPath(AudioCategory category)
    {
        string basePath = Path.Combine(Application.streamingAssetsPath, "Vad", "DetectVoice");
        return Path.Combine(basePath, category.ToString()).Replace("\\", "/");
    }

    private void LoadExistingFiles(AudioCategory category)
    {
        string folderPath = GetFolderPath(category);

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            return;
        }

        string[] existingFiles = Directory.GetFiles(folderPath, "*.wav");
        foreach (string filePath in existingFiles)
        {
            string normalizedPath = filePath.Replace("\\", "/");
            AddToList(normalizedPath, category);
            CreateItemUI(normalizedPath, category);
        }
    }

    public void ImportPositiveWav() { ImportWavFiles(AudioCategory.Positive); }
    public void ImportNegativeWav() { ImportWavFiles(AudioCategory.Negative); }

    private void ImportWavFiles(AudioCategory category)
    {
        // 1. 다이얼로그 열기 전: 클릭 가능하게 변경 & 최상위 해제
        if (_transparentApp != null) _transparentApp.EnableInteractionForDialog();

        // ★ 여기서 윈도우 네이티브 탐색기를 호출합니다. (필터 형식 주의: 설명\0확장자\0)
        string[] paths = WindowsFileBrowser.ShowDialog(
            $"WAV 파일 선택 ({category})", 
            "WAV Files (*.wav)\0*.wav\0All Files (*.*)\0*.*\0"
        );

        // 2. 다이얼로그 닫힌 후: 원래 설정 복구
        if (_transparentApp != null) _transparentApp.RestoreWindowSettings();

        if (paths == null || paths.Length == 0) return;

        string folderPath = GetFolderPath(category);
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        foreach (string sourcePath in paths)
        {
            if (string.IsNullOrEmpty(sourcePath)) continue;

            string fileName = Path.GetFileName(sourcePath);
            string destinationPath = Path.Combine(folderPath, fileName).Replace("\\", "/");

            if (File.Exists(destinationPath) || IsPathInList(destinationPath, category))
            {
                Debug.LogWarning($"[중복] {category}에 이미 존재하는 파일: {fileName}");
                continue; 
            }

            try
            {
                File.Copy(sourcePath, destinationPath, false); 
                AddToList(destinationPath, category);
                CreateItemUI(destinationPath, category);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"에러 발생 ({fileName}): {e.Message}");
            }
        }
    }

    private void CreateItemUI(string path, AudioCategory category)
    {
        Transform targetPanel = (category == AudioCategory.Positive) ? positiveContentPanel : negativeContentPanel;
        GameObject newItem = Instantiate(itemPrefab, targetPanel);
        
        WavItemUI itemUI = newItem.GetComponent<WavItemUI>();
        itemUI.Setup(path, this, category);
    }

    private void AddToList(string path, AudioCategory category)
    {
        if (category == AudioCategory.Positive) positivePaths.Add(path);
        else negativePaths.Add(path);
    }

    private bool IsPathInList(string path, AudioCategory category)
    {
        return category == AudioCategory.Positive ? positivePaths.Contains(path) : negativePaths.Contains(path);
    }

    public void RemoveFromList(string path, AudioCategory category)
    {
        if (category == AudioCategory.Positive && positivePaths.Contains(path)) 
            positivePaths.Remove(path);
        else if (category == AudioCategory.Negative && negativePaths.Contains(path)) 
            negativePaths.Remove(path);
    }
}