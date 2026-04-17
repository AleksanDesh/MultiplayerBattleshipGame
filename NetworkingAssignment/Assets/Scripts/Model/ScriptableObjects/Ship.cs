using System.Collections.Generic;
using UnityEngine;

namespace Model
{
    [RequireComponent(typeof(Collider))]
    public sealed class Ship : MonoBehaviour
    {
        [SerializeField, Range(1, 5)] private int _length = 1;
        [SerializeField] private bool _vertical;
        [SerializeField] private Transform _grabPoint;
        public Transform GrabPoint => _grabPoint;

        private Tile _tile;
        private readonly List<Vector2Int> _occupiedCells = new();

        private Vector3 _pickupPosition;
        private Quaternion _pickupRotation;

        public Tile Tile => _tile;
        public bool IsPlaced => _tile != null;
        public int Length => _length;
        public bool Vertical
        {
            get => _vertical;
            set => _vertical = value;
        }

        public IReadOnlyList<Vector2Int> OccupiedCells => _occupiedCells;

        void Awake()
        {
            if (_grabPoint == null)
                _grabPoint = this.transform.Find("GrabPoint");
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