using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneChanger : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GoToMainRoom()
    {
        Debug.Log("Bắt đầu chuyển sang phòng VR chính...");
        SceneManager.LoadScene("MainVRScene");
    }
}
