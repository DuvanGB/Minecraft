using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
public class WorldGeneratorOptimized : MonoBehaviour
{
    public List<Cell> cubes;    // Available cubes
    public static WorldGeneratorOptimized s; // Singleton
    private List<Vector3> _meshNormals; // List of vectors pointing at the six directions of cubes faces

    private Dictionary<int, Queue<Cell>> _poolDictionary;   // Pools of cubes. For each cube type there will be a pool to take cube instances from

    #region World generation parameters
    [Range(1, 10)]
    private int PATCH_SIZE_X = 1;
    [Range(1, 10)]
    private int PATCH_SIZE_Y = 1;
    [Range(1, 10)]
    private int PATCH_SIZE_Z = 1;

    [HideInInspector]
    public static int CHUNK_SIZE_X = 8;
    [HideInInspector]
    public static int CHUNK_SIZE_Y = 12;
    [HideInInspector]
    public static int CHUNK_SIZE_Z = 8;

    [Range(1, 20)]
    public int N_CHUNKS_X;

    private int N_CHUNKS_Y = 1;
    [Range(1, 20)]
    public int N_CHUNKS_Z;
    [Range(1, 5)]
    public int DRAW_CHUNK_DISTANCE;

    public AnimationCurve heightDensityCurve;

    private World world;
    private Chunk[,,] _map;
    private float _seed;

    [Range(0.0f, 1.0f)]
    private float chunkDensity;
    private float noiseScale = 0.1f;
    #endregion

    #region World drawing
    private int _xDrawLimitLow, _xDrawLimitHigh;
    private int _zDrawLimitLow, _zDrawLimitHigh;

    private int _prevPlayerChunkX;
    private int _prevPlayerChunkZ;
    #endregion

    #region Bit Manipulation Methods
    public long SetBitTo1(long value, int position)
    {
        // Set a bit at position to 1.
        return value |= (1 << position);
    }
    public long SetBitTo0(long value, int position)
    {
        // Set a bit at position to 0.
        return value & ~(1 << position);
    }
    public bool IsBitSetTo1(long value, int position)
    {
        // Return whether bit at position is set to 1.
        return (value & (1 << position)) != 0;
    }
    #endregion
    public bool DrawGizmos = true;

    public Material meshMaterial;
    private void Awake()
    {
        //QualitySettings.vSyncCount = 1;
        //Application.targetFrameRate = 60;
        
        s = this;
        QualitySettings.vSyncCount = 0;  // VSync must be disabled
        Application.targetFrameRate = 30;

        _map = new Chunk[N_CHUNKS_X, 1, N_CHUNKS_Z];
        _seed = UnityEngine.Random.Range(1, 100000);

        #region Mesh Normals init
        _meshNormals = new List<Vector3>();
        _meshNormals.Add(-Vector3.forward);
        _meshNormals.Add(Vector3.forward);
        _meshNormals.Add(-Vector3.right);
        _meshNormals.Add(Vector3.right);
        _meshNormals.Add(Vector3.up);
        _meshNormals.Add(-Vector3.up);
        #endregion
    }
    public Vector3 GetMeshNormal(int index) { return _meshNormals[index]; }

    // Start is called before the first frame update
    void Start()
    {
        UIEventsManager.s.Notify(UIEvents.LOAD_START);
        world = new World();
        UIEventsManager.s.Notify(UIEvents.LOAD_END);
        //GenerateCellPool();
        //GenerateWorld();
        //SpawnPlayer();
    }
    void SpawnPlayer() {
        float centerX = (CHUNK_SIZE_X * N_CHUNKS_X) / 2.0f;
        float centerZ = (CHUNK_SIZE_Z * N_CHUNKS_Z) / 2.0f;

        FPS_Player.s.MoveToPosition(new Vector3(centerX, CHUNK_SIZE_Y + 4, centerZ));

        int playerChunkX = N_CHUNKS_X / 2;
        int playerChunkZ = N_CHUNKS_Z / 2;
     
        _xDrawLimitLow = Mathf.Max(0, playerChunkX - DRAW_CHUNK_DISTANCE);
        _xDrawLimitHigh = Mathf.Min(N_CHUNKS_X, playerChunkX + DRAW_CHUNK_DISTANCE);

        _zDrawLimitLow = Mathf.Max(0, playerChunkZ - DRAW_CHUNK_DISTANCE);
        _zDrawLimitHigh = Mathf.Min(N_CHUNKS_Z, playerChunkZ + DRAW_CHUNK_DISTANCE);

        for (int i = _xDrawLimitLow; i <= _xDrawLimitHigh; ++i) {
            for (int j = _zDrawLimitLow; j <= _zDrawLimitHigh; ++j)
            {
                //UnityEngine.Debug.Log("Drawing chunck " + i + "_" + j);
                
                _map[i, 0, j].Draw();
            }
        }

        _prevPlayerChunkX = playerChunkX;
        _prevPlayerChunkZ = playerChunkZ;
    }
    // Update is called once per frame
    void Update()
    {
        Vector3 pp = FPS_Player.s.GetWorldPosition();
        int currentChunkX = (int)(pp.x) / CHUNK_SIZE_X;
        int currentChunkZ = (int)(pp.z) / CHUNK_SIZE_Z;

        int movedX = currentChunkX - _prevPlayerChunkX;
        int movedZ = currentChunkZ - _prevPlayerChunkZ;
        if (movedX != 0) { 
            
        }

        _prevPlayerChunkZ = currentChunkZ;
        //UnityEngine.Debug.Log("Current chunk: (" + currentChunkX + " - " + currentChunkZ + ")");
    }
    private byte AssignCELL_TYPE(int patchY, int cellY) {
        int N_PATCHES_Y = CHUNK_SIZE_Y / PATCH_SIZE_Y;

        if (patchY == N_PATCHES_Y - 1 && cellY == PATCH_SIZE_Y - 1) return 0;
        else if (patchY == 0 && cellY == 0) return 2;    // Limit cell in the bottom
        else return 1;
    
    }
    public Chunk GetChunkFromPosition(Vector3 p) {
        int cx = ((int)p.x) / CHUNK_SIZE_X;
        int cz = ((int)p.z) / CHUNK_SIZE_Z;

        return _map[cx, 0, cz];
    }
    public bool IsThereCellInPosition(Vector3 p) {

        if (IsInLimits(p))
        {
            Chunk c = GetChunkFromPosition(p);

            int xInChunk = ((int)p.x) % CHUNK_SIZE_X;
            int yInChunk = ((int)p.y) % CHUNK_SIZE_Y;
            int zInChunk = ((int)p.z) % CHUNK_SIZE_Z;

            try
            {
                return c.GetCellFlag(xInChunk, yInChunk, zInChunk);
            }
            catch (NullReferenceException e) {
                UnityEngine.Debug.LogError("Failed to access cell flag");
                return false;
            }
        }
        else return false;
    }
    public Cell GetCellInPosition(Vector3 p) {
        if (IsInLimits(p))
        {
            Chunk c = GetChunkFromPosition(p);

            int xInChunk = ((int)p.x) % CHUNK_SIZE_X;
            int yInChunk = ((int)p.y) % CHUNK_SIZE_Y;
            int zInChunk = ((int)p.z) % CHUNK_SIZE_Z;

            try
            {
                if (c._cells[xInChunk, yInChunk, zInChunk] == null) {
                    if (_poolDictionary[1].Count > 0)
                    {
                        Cell cellToPlace = _poolDictionary[1].Dequeue();
                        cellToPlace.transform.position = p;
                        c._cells[xInChunk, yInChunk, zInChunk] = cellToPlace;
                    }
                    else UnityEngine.Debug.Log("EMPTY POOL.");
                }
                return c._cells[xInChunk, yInChunk, zInChunk];
            }
            catch (NullReferenceException e)
            {
                UnityEngine.Debug.LogError("Failed to access cell flag");
                return null;
            }
        }
        else return null;
    }
    public void DestroyCellInPosition(Vector3 p) {
        if (IsInLimits(p))
        {
            Chunk c = GetChunkFromPosition(p);

            int xInChunk = ((int)p.x) % CHUNK_SIZE_X;
            int yInChunk = ((int)p.y) % CHUNK_SIZE_Y;
            int zInChunk = ((int)p.z) % CHUNK_SIZE_Z;

            try
            {
                c.SetCellFlag(xInChunk, yInChunk, zInChunk, false);  
            }
            catch (NullReferenceException e)
            {
                UnityEngine.Debug.LogError("Failed to destroy cell");  
            }
        }
    }
    public bool IsInLimits(Vector3 pos) {

        if (pos.x >= 0 && pos.x < N_CHUNKS_X * CHUNK_SIZE_X &&
            pos.y >= 0 && pos.y < N_CHUNKS_Y * CHUNK_SIZE_Y &&
            pos.z >= 0 && pos.z < N_CHUNKS_Z * CHUNK_SIZE_Z)
            return true;
        else return false;
    }
    private void GenerateCellPool() {

        GameObject cellPoolGO = new GameObject("CellPool");
        Queue<Cell> _grassPool = new Queue<Cell>();
        Queue<Cell> _groundPool = new Queue<Cell>();
        Queue<Cell> _limitPool = new Queue<Cell>();

        // Generate grass cubes pool
        int howManyGrass = (int)Mathf.Pow(2 * DRAW_CHUNK_DISTANCE + 1, 2) * CHUNK_SIZE_X * 2 * CHUNK_SIZE_Z;
        for (int i = 0; i < howManyGrass; ++i)
        {
            Cell c = Instantiate(cubes[0], Vector3.zero, Quaternion.identity, cellPoolGO.transform);
            c.GetComponent<BoxCollider>().enabled = false;

            _grassPool.Enqueue(c);
        }

        // Generates ground cubes pool
        int howManyGround = (int) Mathf.Pow(2*DRAW_CHUNK_DISTANCE + 1, 2) * CHUNK_SIZE_X * CHUNK_SIZE_Y * CHUNK_SIZE_Z;
        for (int i = 0; i < howManyGround; ++i) {
            Cell c = Instantiate(cubes[1], Vector3.zero, Quaternion.identity, cellPoolGO.transform);
            c.GetComponent<BoxCollider>().enabled = false;

            _groundPool.Enqueue(c);
        }

        int howManyLimit = (int)Mathf.Pow(2 * DRAW_CHUNK_DISTANCE + 1, 2) * CHUNK_SIZE_X * 2 * CHUNK_SIZE_Z;
        for (int i = 0; i < howManyLimit; ++i)
        {
            Cell c = Instantiate(cubes[2], Vector3.zero, Quaternion.identity, cellPoolGO.transform);
            c.GetComponent<BoxCollider>().enabled = false;

            _limitPool.Enqueue(c);
        }
        
        _poolDictionary = new Dictionary<int, Queue<Cell>>();
        _poolDictionary.Add(0, _grassPool);
        _poolDictionary.Add(1, _groundPool);
        _poolDictionary.Add(2, _limitPool);
    }

    public void EnqueueCell(Cell c) { 
        c.Hide();
        c.transform.position = Vector3.zero;
        _poolDictionary[c.type].Enqueue(c);
    }

}
