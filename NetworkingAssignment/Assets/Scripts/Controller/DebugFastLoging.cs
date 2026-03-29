using TMPro;
using UnityEngine;

public class DebugFastLoging : MonoBehaviour
{
    public string username;
    public string userPassword;
    public TMP_InputField uname;
    public TMP_InputField password;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        uname.text = username;
        password.text = userPassword;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
