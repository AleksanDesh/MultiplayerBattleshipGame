using TMPro;
using UnityEngine;

public class DebugFastLoging : MonoBehaviour
{
    public string username;
    public string userPassword;

    public string username2;
    public string userPassword2;

    public TMP_InputField uname;
    public TMP_InputField password;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
#if UNITY_EDITOR
        uname.text = username;
        password.text = userPassword;
#else
        uname.text = username2;
        password.text = userPassword2;
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
