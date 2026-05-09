using System.Collections.Generic;
using UnityEngine;

namespace Model
{
    [RequireComponent(typeof(Collider))]
    public sealed class Mine : MonoBehaviour
    {
        private static int _idCounter = 0;

        public int Id { get; private set; }

        [SerializeField] private Transform _grabPoint;
        public Transform GrabPoint => _grabPoint;

        private Tile _tile;
        private readonly List<Vector2Int> _occupiedCells = new();

        private Vector3 _pickupPosition;
        private Quaternion _pickupRotation;

        public Tile Tile => _tile;
        public bool IsPlaced => _tile != null;
        public IReadOnlyList<Vector2Int> OccupiedCells => _occupiedCells;

        private void Awake()
        {
            if (_grabPoint == null)
                _grabPoint = transform.Find("GrabPoint");

            if (_grabPoint == null)
                _grabPoint = transform;

            Id = ++_idCounter;
        }

        public void BeginPickup()
        {
            _pickupPosition = transform.position;
            _pickupRotation = transform.rotation;
        }

        public void RestorePickupTransform()
        {
            transform.SetPositionAndRotation(_pickupPosition, _pickupRotation);
        }

        public void SetPlacement(Tile anchorTile, IEnumerable<Vector2Int> occupiedCells)
        {
            _tile = anchorTile;
            _occupiedCells.Clear();
            _occupiedCells.AddRange(occupiedCells);
        }

        public void ClearPlacement()
        {
            _tile = null;
            _occupiedCells.Clear();
        }
    }
}