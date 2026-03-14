using UnityEngine;

public class PlayAnimation : MonoBehaviour
{
    Animator anim;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>();
        anim.Play("HumanArmature|Man_Sitting", 0, 0);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
