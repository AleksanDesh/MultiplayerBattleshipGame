using Model;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    public int _with, _height;

    [SerializeField] private GameObject _tilePrefab;

    Dictionary<Vector2Int, Tile> _locTileKey = new Dictionary<Vector2Int, Tile>();
    HashSet<Ship> _placedShips = new HashSet<Ship>();

    private void Start()
    {
        GenerateGrid();
    }
    void GenerateGrid()
    {
        DestroyAllTiles();
        for (int x = 0; x < _height; x++)
        {
            for (int y = 0; y < _with; y++)
            {
                var spawnedObject = Instantiate(_tilePrefab, new Vector3(x, 0, y), Quaternion.identity, this.transform);
                spawnedObject.name = $"Tile {x} {y}";
                bool isOffset = ((x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0));
                spawnedObject.GetComponent<Tile>().Init(new Vector2Int(x, y), isOffset);
                _locTileKey.Add(new Vector2Int(x, y), spawnedObject.GetComponent<Tile>());
            }
        }
    }

    public bool TryGetTile(Vector2Int position, out Tile tile)
    {
        if (_locTileKey.TryGetValue(position, out tile))
            return true;
        return false;
    }
    public bool CanPlaceShip(Vector2Int origin, int length, bool vertical)
    {
        var shipCells = GetShipCells(origin, length, vertical);

        foreach (var cell in shipCells)
        {
            if (!TryGetTile(cell, out var tile))
                return false;

            if (tile.IsOccupied)
                return false;
        }

        foreach (var cell in shipCells)
        {
            foreach (var neighbor in GetNeighbors(cell))
            {
                if (!TryGetTile(neighbor, out var tile))
                    continue;

                if (tile.IsOccupied && !shipCells.Contains(neighbor))
                    return false;
            }
        }

        return true;
    }
    public void ShowInvalidPlaces(Ship movedShip)
    {
        foreach (Ship ship in _placedShips)
        {
            if (ship != movedShip)
                EncolorShipBoundaries(ship);
        }
    }

    void EncolorShipBoundaries(Ship ship)
    {
        var shipCells = GetShipCells(ship.Tile.Coord, ship.Length, ship.Vertical);
        foreach (var cell in shipCells)
        {
            foreach (var neighbor in GetNeighbors(cell))
            {
                if (!TryGetTile(neighbor, out var tile))
                    continue;

                if (!tile.IsOccupied && !shipCells.Contains(neighbor))
                    tile.Highlight(true);
            }
        }
    }

    public bool TryPlaceShip(Ship ship, Vector2Int origin, bool vertical)
    {
        if (ship == null)
            return false;

        var occupiedCells = GetShipCells(origin, ship.Length, vertical);

        if (!CanPlaceShip(origin, ship.Length, vertical))
            return false;

        foreach (var cell in occupiedCells)
        {
            if (TryGetTile(cell, out var tile))
                tile.SetOccupied(true);
        }

        if (!TryGetTile(origin, out var anchorTile))
            return false;

        ship.Vertical = vertical;
        ship.SetPlacement(anchorTile, occupiedCells);

        Vector3 shipPos = anchorTile.transform.position;
        ship.transform.position = new Vector3(shipPos.x, ship.transform.position.y, shipPos.z);

        if (!_placedShips.Contains(ship))
            _placedShips.Add(ship);

        return true;
    }

    private List<Vector2Int> GetShipCells(Vector2Int origin, int length, bool vertical)
    {
        Vector2Int step = vertical ? Vector2Int.up : Vector2Int.right;
        int startOffset = -(length / 2);

        List<Vector2Int> cells = new(length);

        for (int i = 0; i < length; i++)
        {
            Vector2Int coord = origin + step * (startOffset + i);
            cells.Add(coord);
        }

        return cells;
    }

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int cell)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                yield return new Vector2Int(cell.x + x, cell.y + y);
            }
        }
    }

    public void ClearShip(Ship ship)
    {
        if (ship == null)
            return;

        foreach (var coord in ship.OccupiedCells)
        {
            if (TryGetTile(coord, out var tile))
                tile.SetOccupied(false);
        }

        ship.ClearPlacement();
    }

    public void RestoreShip(Ship ship, Vector2Int origin, bool vertical)
    {
        if (ship == null)
            return;

        TryPlaceShip(ship, origin, vertical);
    }

    public void ClearAllPreview()
    {
        foreach (var tile in _locTileKey.Values)
            tile.SetPreview(false);
    }

    public void ClearAllHighlight()
    {
        foreach (var tile in _locTileKey.Values)
        {
            if (tile.Highlighted)
            {
                tile.Highlight(false);
            }
        }
    }

    public void DestroyAllTiles()
    {
        foreach (var tile in _locTileKey.Values)
            Destroy(tile.gameObject);
        _locTileKey.Clear();
    }
}
