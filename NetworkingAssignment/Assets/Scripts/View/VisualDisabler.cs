using Network;
using UnityEngine;
using static Network.Client;

public class VisualDisabler : MonoBehaviour
{
    private GameObject _shipPreset;
    private GridManager _grid;
    private bool _subscribed;

    public void DisableOnStartBattle(GameObject go, GridManager gm)
    {
        _shipPreset = go;
        _grid = gm;
    }

    private void OnEnable()
    {
        if (Client.Instance != null)
        {
            Client.Instance.OnBattleStarted += DisableVisual;
            _subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (_subscribed && Client.Instance != null)
        {
            Client.Instance.OnBattleStarted -= DisableVisual;
        }

        _subscribed = false;
    }

    private void OnDestroy()
    {
        if (_subscribed && Client.Instance != null)
        {
            Client.Instance.OnBattleStarted -= DisableVisual;
        }
    }

    private void DisableVisual(BattleStartPckg pckg)
    {
        if (_grid == null)
        {
            Debug.LogError("VisualDisabler: _grid is null.");
            return;
        }

        if (_shipPreset == null)
        {
            Debug.LogError("VisualDisabler: _shipPreset is null.");
            return;
        }

        try
        {
            _grid.ClearGridVisually();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"VisualDisabler: ClearGridVisually failed: {e}");
            return;
        }

        _shipPreset.SetActive(false);
    }
}