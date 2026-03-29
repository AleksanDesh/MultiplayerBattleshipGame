using UnityEngine;

namespace Model
{
    public class LoginUnityTest : MonoBehaviour
    {
        Login _login;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _login = FindFirstObjectByType<Server>().Login;
            _login.LoginOrCreate("PC200", "kniga");
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}