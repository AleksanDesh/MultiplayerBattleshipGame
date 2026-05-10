using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
/// <summary>
/// Used to display messages across scenes.
/// </summary>
public class GlobalMessageUI : MonoBehaviour
{
    public static GlobalMessageUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        closeButton.onClick.AddListener(Hide);

        Hide();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
        }
    }

    public void Show(string message)
    {
        messageText.text = message;
        panel.SetActive(true);
    }

    public void Hide()
    {
        StartCoroutine(HideAfterFrame());
    }

    IEnumerator HideAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        panel.SetActive(false);
    }
}