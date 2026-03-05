using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class WavItemUI : MonoBehaviour
{
    public TMP_Text fileNameText;
    public Button deleteButton;

    private string filePath;
    private WavImporter mainImporter;
    private AudioCategory myCategory; // ★ 자신이 속한 카테고리 기억

    // Setup에 AudioCategory 매개변수 추가
    public void Setup(string path, WavImporter importer, AudioCategory category)
    {
        filePath = path;
        mainImporter = importer;
        myCategory = category; 
        
        fileNameText.text = Path.GetFileName(path);
        deleteButton.onClick.AddListener(DeleteFile);
    }

    private void DeleteFile()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            string metaPath = filePath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
        }

        // 삭제할 때 자신이 속한 카테고리 정보도 같이 넘겨줌
        mainImporter.RemoveFromList(filePath, myCategory); 
        Destroy(gameObject);
    }
}