using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Collider))]
public class Tile : MonoBehaviour
{
    [SerializeField] private Color _baseColor = Color.aliceBlue;
    [SerializeField] private Color _offsetColor = Color.blueViolet;
    [SerializeField] private Color _selectedColor = Color.coral;
    [SerializeField] private Color _occupiedColor = Color.indianRed;
    [SerializeField] private Color _highlightedColor = Color.indianRed;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Color _originalColor;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

    private bool _occupied;
    private bool _highlighted;
    public bool Highlighted => _highlighted;
    private bool _previewActive = true;
    public Vector2Int Coord { get; private set; }
    public bool IsOccupied => _occupied;
    bool _isEnemyTile;
    public bool IsEnemyTile => _isEnemyTile;
    public State CurrentState = State.Emtpy;

    public enum State
    {
        Emtpy,
        Bombed,
        Destroyed
    }

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    public void Init(Vector2Int coord, bool isOffset, bool IsEnemyTile)
    {
        _originalColor = isOffset ? _offsetColor : _baseColor;
        SetColor(_originalColor);
        Coord = coord;
        this._isEnemyTile = IsEnemyTile;
    }

    public void SetOccupied(bool value)
    {
        _occupied = value;
        Debug.Log($"Setting tile {name} to be occupied {_occupied}, with preview being {_previewActive}");
        Color newCol = _occupied ? _occupiedColor : _originalColor;
        SetColor(newCol);
    }

    public void Highlight(bool value)
    {
        _highlighted = value;
        SetColor(_highlighted ?  _highlightedColor : _originalColor);

    }

    public void SetPreview(bool valid)
    {
        _previewActive = valid;
    }


    private void OnMouseEnter()
    {
        if (_previewActive && !_occupied)
            SetColor(_selectedColor);
    }

    private void OnMouseExit()
    {
        if (_previewActive && !_occupied)
            SetColor(_originalColor);
    }

    private void SetColor(Color color)
    {
        //Debug.Log($"Setting color of {name} to {color}");
        _renderer.GetPropertyBlock(_mpb);

        _mpb.SetColor(BaseColorId, color);
        _mpb.SetColor(ColorId, color);

        _renderer.SetPropertyBlock(_mpb);
    }
}