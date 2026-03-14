using Assets.CustomPdfViewer.Scripts;
using NativeFilePickerNamespace;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

public class GameModeManager : MonoBehaviour
{
    public GameObject gameMenu; 
    public Renderer screenRenderer; 
    public CustomPdfViewerUI pdfViewer; 
    public InputActionReference nextSlideInput;
    public InputActionReference previousSlideInput;
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
    private void OnNextSlidePressed(InputAction.CallbackContext context)
    {
        NextSlide(); // Calls your existing method!
    }

    private void OnPreviousSlidePressed(InputAction.CallbackContext context)
    {
        PreviousSlide(); // Calls your existing method!
    }
    private void OnEnable()
    {
        // When the object turns on, start listening to the controllers
        if (nextSlideInput != null)
            nextSlideInput.action.performed += OnNextSlidePressed;

        if (previousSlideInput != null)
            previousSlideInput.action.performed += OnPreviousSlidePressed;
    }

    private void OnDisable()
    {
        // When the object turns off, stop listening (prevents crashes)
        if (nextSlideInput != null)
            nextSlideInput.action.performed -= OnNextSlidePressed;

        if (previousSlideInput != null)
            previousSlideInput.action.performed -= OnPreviousSlidePressed;
    }
}
