using NativeFilePickerNamespace;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Assets.CustomPdfViewer.Scripts;

public class GameModeManager : MonoBehaviour
{
    public GameObject gameMenu; public Renderer screenRenderer; public CustomPdfViewerUI pdfViewer;
    public void OpenFilePicker()
    {
        // Change the filter to only accept PDFs
        string[] fileTypes = { "application/pdf" };

        NativeFilePicker.PickFile((path) =>
        {
            if (path == null)
            {
                Debug.Log("User closed the picker.");
            }
            else
            {
                Debug.Log("Successfully picked PDF: " + path);

                // Pass the absolute path directly to the package
                pdfViewer.LoadPDF(path, true);
            }
        }, fileTypes);
    }
    public void NextSlide()
    {
        if (pdfViewer != null)
        {
            pdfViewer.NextPage();
        }
    }

    public void PreviousSlide()
    {
        if (pdfViewer != null)
        {
            pdfViewer.PreviousPage();
        }
    }
    public void StartExam()
    {
        pdfViewer.LoadPDF("document.pdf");
        Debug.Log("Exam Mode Activated!"); HideMenu();
    }
    public void StartPractice()
    {
        Debug.Log("Practice Mode Activated!"); HideMenu();
    }

    private void HideMenu()
    {
        if (gameMenu != null)
        {
            gameMenu.SetActive(false);
        }
    }
}
