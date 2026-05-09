using Controller;
using Model;
using OSCTools;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using static Network.Client;

public class GridPlacement : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private GridManager _grid;
    [SerializeField] private GridManager _enemyGrid;
    [SerializeField] private LayerMask _tileMask = ~0;
    [SerializeField] private LayerMask _shipMask = ~0;
    [SerializeField] private float _dragHeight = 0.5f;
    
    public bool IgnoreServer;
    private SeaBattleClientController _controller;
    private Ship _draggedShip;
    private Mine _draggedMine;

    private Vector3 _pickupPosition;
    private Quaternion _pickupRotation;
    private Tile _pickupTile;
    private bool _pickupVertical;

    private bool _dragEnabled = true;
    bool _bombing = false; // if currently waiting for bomb result from server
    bool _isMyTurn;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
        _controller = FindFirstObjectByType<SeaBattleClientController>();
    }

    /// <summary>
    /// If you disable drag, this script will listen to bombing. Else dragging ships
    /// </summary>
    public void EnableDrag(bool state)
    {
        _dragEnabled = state;
    }

    public void UpdateGrids()
    {
        if (_dragEnabled)
        {
            if (_draggedShip == null && _draggedMine == null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (TryGetShipUnderPointer(out var ship))
                        BeginDrag(ship);
                    else if (TryGetMineUnderPointer(out var mine))
                        BeginDrag(mine);
                }

                return;
            }

            UpdateDraggedPiece();

            if (Input.GetMouseButtonUp(0))
                EndDrag();
        }
        else
        {
            if (_isMyTurn && Input.GetMouseButtonDown(0) && TryGetTileUnderPointer(out var tile))
            {
                if (tile.IsEnemyTile)
                    BombTile(tile);
            }
        }
    }

    private void BeginDrag(Ship ship)
    {
        _draggedShip = ship;
        _draggedShip.BeginPickup();

        _grid.ShowInvalidPlaces(ship);

        _pickupPosition = ship.transform.position;
        _pickupRotation = ship.transform.rotation;
        _pickupTile = ship.Tile;
        _pickupVertical = ship.Vertical;

        if (_pickupTile != null)
            _grid.ClearShip(ship);

        _grid.SetPreview(false);
    }
    private void BeginDrag(Mine mine)
    {
        _draggedMine = mine;
        _draggedMine.BeginPickup();


        _pickupPosition = mine.transform.position;
        _pickupRotation = mine.transform.rotation;
        _pickupTile = mine.Tile;

        if (_pickupTile != null)
            _grid.ClearMine(mine);
    }
    private void UpdateDraggedPiece()
    {
        if (!TryGetMouseWorldPoint(out var mouseWorld))
            return;

        if (_draggedShip != null && Input.GetMouseButtonDown(1))
        {
            _draggedShip.Vertical = !_draggedShip.Vertical;

            if (_draggedShip.Vertical)
                _draggedShip.transform.Rotate(0f, -90f, 0f, Space.World);
            else
                _draggedShip.transform.Rotate(0f, 90f, 0f, Space.World);

            _grid.ClearAllHighlight();
            _grid.ShowInvalidPlaces(_draggedShip);
        }

        Vector3 target = mouseWorld;
        target.y = _dragHeight;

        if (_draggedShip != null)
        {
            Vector3 delta = _draggedShip.GrabPoint.position - _draggedShip.transform.position;
            _draggedShip.transform.position = target - delta;
        }
        else if (_draggedMine != null)
        {
            Vector3 delta = _draggedMine.GrabPoint.position - _draggedMine.transform.position;
            _draggedMine.transform.position = target - delta;
        }
    }


    private async void BombTile(Tile tile)
    {
        if (_bombing) return;
        if (tile.CurrentState != Tile.State.Emtpy) return;
        _bombing = true;
        _isMyTurn = false;
        if (!IgnoreServer)
        {
            bool serverAccepted = await _controller.Bomb(tile.Coord.x, tile.Coord.y);
            if (!serverAccepted)
            {
                Debug.Log("Server did not accept bombing position, restoring");
                _isMyTurn = true;
            }
        } 

       
       _bombing = false;
    }

    private async void EndDrag()
    {
        if (_draggedShip != null)
        {
            await EndShipDrag();
            return;
        }

        if (_draggedMine != null)
        {
            await EndMineDrag();
        }
    }
    private async Task EndShipDrag()
    {
        if (_draggedShip == null)
            return;

        bool placed = false;
        Tile targetTile = null;

        if (TryGetTileUnderPointer(out var tile))
        {
            targetTile = tile;
            placed = _grid.TryPlaceShip(_draggedShip, tile.Coord, _draggedShip.Vertical);
        }

        if (!placed)
        {
            _draggedShip.transform.SetPositionAndRotation(_pickupPosition, _pickupRotation);

            if (_pickupTile != null)
                _grid.RestoreShip(_draggedShip, _pickupTile.Coord, _pickupVertical);
            else
                _draggedShip.ClearPlacement();

            _draggedShip.Vertical = _pickupVertical;
            _draggedShip = null;
            _grid.ClearAllHighlight();
            return;
        }

        var tmpDraggedShip = _draggedShip;
        _draggedShip = null;

        if (!IgnoreServer)
        {
            bool serverAccepted = await _controller.PlaceShip(
                tmpDraggedShip,
                targetTile.Coord.x,
                targetTile.Coord.y
            );

            if (!serverAccepted)
            {
                _grid.ClearShip(tmpDraggedShip);

                tmpDraggedShip.transform.SetPositionAndRotation(_pickupPosition, _pickupRotation);
                tmpDraggedShip.Vertical = _pickupVertical;

                if (_pickupTile != null)
                    _grid.RestoreShip(tmpDraggedShip, _pickupTile.Coord, _pickupVertical);
                else
                    tmpDraggedShip.ClearPlacement();
            }
        }

        _grid.ClearAllHighlight();
        _grid.SetPreview(true);
    }

    private async Task EndMineDrag()
    {
        if (_draggedMine == null)
            return;

        bool placed = false;
        Tile targetTile = null;

        if (TryGetTileUnderPointer(out var tile))
        {
            targetTile = tile;
            placed = _grid.TryPlaceMine(_draggedMine, tile.Coord);
        }

        if (!placed)
        {
            _draggedMine.transform.SetPositionAndRotation(_pickupPosition, _pickupRotation);

            if (_pickupTile != null)
                _grid.RestoreMine(_draggedMine, _pickupTile.Coord);
            else
                _draggedMine.ClearPlacement();

            _draggedMine = null;
            _grid.ClearAllHighlight();
            return;
        }

        var tmpDraggedMine = _draggedMine;
        _draggedMine = null;

        if (!IgnoreServer)
        {
            bool serverAccepted = await _controller.PlaceMine(
                tmpDraggedMine,
                targetTile.Coord.x,
                targetTile.Coord.y
            );

            if (!serverAccepted)
            {
                _grid.ClearMine(tmpDraggedMine);

                tmpDraggedMine.transform.SetPositionAndRotation(_pickupPosition, _pickupRotation);

                if (_pickupTile != null)
                    _grid.RestoreMine(tmpDraggedMine, _pickupTile.Coord);
                else
                    tmpDraggedMine.ClearPlacement();
            }
        }

        _grid.ClearAllHighlight();
        _grid.SetPreview(true);
    }

    private bool TryGetShipUnderPointer(out Ship ship)
    {
        ship = null;

        if (_camera == null)
            return false;

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _shipMask))
            return false;

        return hit.collider.TryGetComponent(out ship);
    }
    private bool TryGetMineUnderPointer(out Mine mine)
    {
        mine = null;

        if (_camera == null)
            return false;

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _shipMask))
            return false;

        return hit.collider.TryGetComponent(out mine);
    }
    private bool TryGetTileUnderPointer(out Tile tile)
    {
        tile = null;

        if (_camera == null)
            return false;

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, _tileMask))
            return false;

        return hit.collider.TryGetComponent(out tile);
    }

    private bool TryGetMouseWorldPoint(out Vector3 point)
    {
        point = default;

        if (_camera == null)
            return false;

        Plane plane = new Plane(Vector3.up, new Vector3(0f, _dragHeight, 0f));
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (!plane.Raycast(ray, out float enter))
            return false;

        point = ray.GetPoint(enter);
        return true;
    }

    #region serverPackages
    private void OnEnable()
    {
        _controller.NetworkClient.OnBombing += UpdateTurn;
        _controller.NetworkClient.OnBattleStarted += StartBattle;

    }

    private void OnDisable()
    {
        _controller.NetworkClient.OnBombing -= UpdateTurn;
        _controller.NetworkClient.OnBattleStarted -= StartBattle;
    }

    void UpdateTurn(Bombpckg pckg)
    {// if sucessefully bombed enemy tile (0) or mine (4), make one more turn
        if ((pckg.result == 0 || pckg.result == 4) && pckg.IsForEnemy)
            _isMyTurn = true;
        // if was not my turn and it failed => my turn (ignore result 6)
        if ((pckg.result != 0 && pckg.result != 4) && !pckg.IsForEnemy)
            _isMyTurn = true;
        
    }

    void StartBattle(BattleStartPckg pckg)
    {
        _isMyTurn = pckg.Turn;
    }

    #endregion
}
