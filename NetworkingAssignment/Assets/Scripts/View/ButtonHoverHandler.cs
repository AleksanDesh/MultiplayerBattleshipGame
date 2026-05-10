using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private int GridSize = 1;
    [SerializeField] private GridManager Grid;
    [SerializeField] private GameObject ShipsPreset;
    [SerializeField] private Transform PrestSpawnLocation;
    private GameObject spawnedPreset;
    void Awake()
    {
        spawnedPreset = Instantiate(ShipsPreset, PrestSpawnLocation.position, PrestSpawnLocation.rotation);
        spawnedPreset.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //Debug.Log("Mouse entered button");
        Grid.DisplayGridVisually(GridSize, GridSize);
        spawnedPreset.SetActive(true);

        // hover logic here
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //Debug.Log("Mouse exited button");
        Grid.ClearGridVisually();
        spawnedPreset.SetActive(false);
        // exit logic here
    }
}