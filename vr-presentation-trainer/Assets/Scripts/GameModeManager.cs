using System;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public GameObject gameMenu;
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
}
