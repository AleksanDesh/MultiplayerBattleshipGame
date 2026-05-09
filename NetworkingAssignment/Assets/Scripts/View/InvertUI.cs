using UnityEngine;

public class InvertUI : MonoBehaviour
{
    public void InvertActive(GameObject go)
    {
        go.SetActive(!go.activeSelf);
    }
}
