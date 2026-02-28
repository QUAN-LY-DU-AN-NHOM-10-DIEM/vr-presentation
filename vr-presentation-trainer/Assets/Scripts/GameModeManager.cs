using NativeFilePickerNamespace;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class GameModeManager : MonoBehaviour
{
    public GameObject gameMenu; public Renderer screenRenderer;
    public void StartExam()
    {
        Debug.Log("Exam Mode Activated!"); HideMenu();
    }
    public void StartPractice()
    {
        Debug.Log("Practice Mode Activated!"); HideMenu();
    }

    public void OpenFilePicker()
    {
        // Define filters (e.g., images)
        string[] fileTypes = { "image/*" };

        // In 1.4.1, this returns void, so we don't assign it to a variable
        NativeFilePicker.PickFile((path) =>
        {
            if (path == null)
            {
                Debug.Log("User closed the picker without choosing a file.");
            }
            else
            {
                Debug.Log("Successfully picked: " + path);
                StartCoroutine(LoadAndRenderImage(path));
            }
        }, fileTypes);
    }

    private IEnumerator LoadAndRenderImage(string path)
    {
        // Convert local path to URI format
        string uri = "file://" + path;

        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(uri))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load image: " + uwr.error);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);

                if (screenRenderer != null)
                {
                    // Apply the texture to the board's screen
                    screenRenderer.material.mainTexture = texture;
                    Debug.Log("Image successfully rendered on screen.");
                }
            }
        }
    }

    private void HideMenu()
    {
        if (gameMenu != null)
        {
            gameMenu.SetActive(false);
        }
    }
}
