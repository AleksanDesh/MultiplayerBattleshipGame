using Network;
using UnityEngine;
using static Network.Client;

public class VisualDisabler : MonoBehaviour
{
    private GameObject _shipPreset;
    private GridManager _grid;

    public void DisableOnStartBattle(GameObject go, GridManager gm)
    {
        _shipPreset = go;
        _grid = gm;
    }
    private void OnEnable()
    {
        Client.Instance.OnBattleStarted += DisableVisual;
    }

    private void OnDisable()
    {
        Client.Instance.OnBattleStarted -= DisableVisual;
    }

    private void OnDestroy()
    {
        Client.Instance.OnBattleStarted -= DisableVisual;
    }

    private void DisableVisual(BattleStartPckg pckg)
    {
        _grid.ClearGridVisually();
        _shipPreset.SetActive(false);
    }
}
