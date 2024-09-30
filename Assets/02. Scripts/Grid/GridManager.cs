using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public enum TileType
{
    Empty,
    Possible,
    Impossible,
}

public class GridManager : ManagerBase, IEventListener
{
    [SerializeField] private Tilemap _checkTilemap;
    [SerializeField] private Tilemap _markTilemap;
    [SerializeField] private TileBase[] _tileBases;

    private GridLayout _gridLayout;

    private GridObject _currentObject;
    private Vector3Int _currentCellPosition;
    private TileBase[] _currentTiles;

    private bool _isEditing = false;
    private bool _isDragging = false;

    private EditCirclePanel _editCircle;

    private void Start()
    {
        App.Instance.GetManager<EventManager>().AddListener(EventCode.EditStart, this);
        App.Instance.GetManager<EventManager>().AddListener(EventCode.EditEnd, this);

        _gridLayout = GetComponent<GridLayout>();
        _editCircle = App.Instance.GetManager<UIManager>().GetPanel<EditCirclePanel>();
    }

    private void Update()
    {
        if (!_isEditing)
        { 
            return;
        }

        HandleInput();
    }

    private void HandleInput()
    {
        Vector2 touchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int cellPosition = _gridLayout.WorldToCell(touchPosition);

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            _isDragging = true;

            RaycastHit2D hit = Physics2D.Raycast(touchPosition, Vector2.zero);

            if (hit.collider != null && hit.collider.CompareTag("GridObject"))
            {
                GridObject gridObject = hit.collider.GetComponentInParent<GridObject>();

                if (_currentObject == null || gridObject != _currentObject)
                {
                    SetObject(gridObject);
                }
            }
            else
            {
                if (_currentObject != null && _currentCellPosition != cellPosition)
                {
                    SetObjectPosition(cellPosition);
                    FollowObject();
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
        }

        if (_isDragging && _currentObject != null)
        {
            if (_currentCellPosition != cellPosition)
            {
                SetObjectPosition(cellPosition);
                FollowObject();
            }
        }
    }

    private void SetObjectPosition(Vector3Int cellPosition)
    {
        _currentObject.transform.position = _gridLayout.CellToWorld(cellPosition) + _gridLayout.cellSize * 0.5f;
        _currentCellPosition = cellPosition;
    }

    private void FollowObject()
    {
        ClearArea();

        _currentObject.Area.position = _gridLayout.WorldToCell(_currentObject.transform.position);
        _editCircle.SetPosition(_currentObject.transform);

        BoundsInt buildingArea = _currentObject.Area;
        TileBase[] baseArray = _checkTilemap.GetTilesBlock(buildingArea);
        Debug.Log("Base array length: " + baseArray.Length);
        TileBase[] tileArray = new TileBase[baseArray.Length];

        for (int i = 0; i < baseArray.Length; i++)
        {
            if (baseArray[i] == _tileBases[(int)TileType.Empty])
            {
                tileArray[i] = _tileBases[(int)TileType.Possible];
                Debug.Log("Possible tile at index: " + i);
            }
            else
            {
                tileArray[i] = _tileBases[(int)TileType.Impossible];
                Debug.LogError("Possible tile at index: " + i);
            }
        }

        _currentTiles = tileArray;
        _markTilemap.SetTilesBlock(buildingArea, tileArray);
    }

    private void ClearArea()
    {
        if (_currentObject == null)
        {
            return;
        }

        BoundsInt area = _currentObject.Area;
        TileBase[] emptyTiles = new TileBase[area.size.x * area.size.y * area.size.z];
        _markTilemap.SetTilesBlock(area, emptyTiles);
    }

    #region Manage Object
    public void SetObject(GridObject gridObject, bool isPressing = true)
    {
        if (_currentObject != null)
        {
            CancelObject();
        }

        _currentObject = gridObject;
        _currentObject.StartEdit();
        SetObjectPosition(_currentObject.Area.position);
        _editCircle.OpenPanel();

        _isDragging = isPressing;

        FollowObject();
    }

    public void ConfirmObject()
    {
        if (_currentObject == null) 
        {
            return;
        }

        if (CheckCanPlace())
        {
            _currentObject.Place();
            ClearArea();

            _currentObject = null;
            _editCircle.ClosePanel();
        }
    }

    public void CancelObject(bool keepObject = false)
    {
        ClearArea();

        if (!_currentObject.IsPlaced || keepObject)
        {
            App.Instance.GetManager<UIManager>().GetPanel<StoragePanel>().AddObjectCount(_currentObject.name, 1);
            Destroy(_currentObject.gameObject);
        }
        else
        {
            SetObjectPosition(_currentObject.PreviousArea.position);
            _currentObject.EndEdit();
        }

        _currentObject = null;
        _currentTiles = null;
        _editCircle.ClosePanel();
    }

    public void RotateObject()
    {
        _currentObject.Rotate();
        FollowObject();
    }
    #endregion

    private bool CheckCanPlace()
    {
        foreach (var tile in _currentTiles)
        {
            if (tile == _tileBases[(int)TileType.Impossible])
            {
                return false;
            }
        }

        return true;
    }

    public void OnEvent(EventCode code, Component sender, object param = null)
    {
        switch (code)
        {
            case EventCode.EditStart:
                _isEditing = true;
                break;

            case EventCode.EditEnd:
                _isEditing = false;
                if (_currentObject != null)
                {
                    CancelObject();
                }
                break;
        }
    }
}
