using UnityEngine;

namespace Model
{
    public class LoginUnityTest : MonoBehaviour
    {
        Login _login;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _login = GameObject.FindAnyObjectByType<Server>().Login;
            _login.LoginOrCreate("minekillerr", "123456789");
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}