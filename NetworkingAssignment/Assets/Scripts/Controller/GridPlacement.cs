using Controller;
using Model;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;

public class GridPlacement : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private GridManager _grid;
    [SerializeField] private LayerMask _tileMask = ~0;
    [SerializeField] private LayerMask _shipMask = ~0;
    [SerializeField] private float _dragHeight = 0.5f;
    public bool IgnoreServer;
    private SeaBattleClientController _controller;
    private Ship _draggedShip;

    private Vector3 _pickupPosition;
    private Quaternion _pickupRotation;
    private Tile _pickupTile;
    private bool _pickupVertical;

    private bool _dragEnabled = true;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
        _controller = FindFirstObjectByType<SeaBattleClientController>();
    }

    public void UpdateDragging()
    {
        if (!_dragEnabled) return;
        if (_draggedShip == null)
        {
            if (Input.GetMouseButtonDown(0) && TryGetShipUnderPointer(out var ship))
                BeginDrag(ship);

            return;
        }

        UpdateDraggedShip();

        if (Input.GetMouseButtonUp(0))
            EndDrag();
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

        //if (TryGetMouseWorldPoint(out var mouseWorld))
        //    _dragOffset = ship.transform.position - mouseWorld;
        //else
        //    _dragOffset = Vector3.zero;
    }

    private void UpdateDraggedShip()
    {
        if (!TryGetMouseWorldPoint(out var mouseWorld))
            return;

        if (Input.GetMouseButtonDown(1))
        {
            _draggedShip.Vertical = !_draggedShip.Vertical;

            if (_draggedShip.Vertical)
                _draggedShip.transform.Rotate(0f, -90f, 0f, Space.World);
            else
                _draggedShip.transform.Rotate(0f, 90f, 0f, Space.World);

            // Realign after rotation
            //if (TryGetMouseWorldPoint(out var mouseWorld))
            //{
            //    Vector3 target2 = mouseWorld;
            //    target2.y = _dragHeight;

            //    Vector3 delta2 = _draggedShip.GrabPoint.position - _draggedShip.transform.position;
            //    _draggedShip.transform.position = target2 - delta2;
            //}

            _grid.ClearAllHighlight();
            _grid.ShowInvalidPlaces(_draggedShip);
        }

        Vector3 target = mouseWorld;
        target.y = _dragHeight;

        Vector3 delta = _draggedShip.GrabPoint.position - _draggedShip.transform.position;
        _draggedShip.transform.position = target - delta;
    }

    private async void EndDrag()
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
        // If draggedShip is not null will update location while this waits for a response
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
                Debug.Log("Server did not accept position, restoring");
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
}
