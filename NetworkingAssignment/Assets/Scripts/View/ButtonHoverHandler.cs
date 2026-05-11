using Network;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private int GridSize = 1;
    [SerializeField] private GridManager Grid;
    [SerializeField] private GameObject ShipsPreset;
    [SerializeField] private Transform PrestSpawnLocation;
    private GameObject _spawnedPreset;
    private VisualDisabler _disabler;
    void Awake()
    {
        _spawnedPreset = Instantiate(ShipsPreset, PrestSpawnLocation.position, PrestSpawnLocation.rotation);
        _spawnedPreset.SetActive(false);
        this.gameObject.GetComponent<Button>().onClick.AddListener(ForwardForDisabling);
        _disabler = FindFirstObjectByType<VisualDisabler>();
        if (_disabler == null)
            Debug.LogWarning("VisualDisabler is null");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //Debug.Log("Mouse entered button");
        Grid.DisplayGridVisually(GridSize, GridSize);
        _spawnedPreset.SetActive(true);

        // hover logic here
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //Debug.Log("Mouse exited button");
        Grid.ClearGridVisually();
        _spawnedPreset.SetActive(false);
        // exit logic here
    }

   void ForwardForDisabling()
    {
        _disabler.DisableOnStartBattle(_spawnedPreset, Grid);
    }


}