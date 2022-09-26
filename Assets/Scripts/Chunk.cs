using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk {
    ChunkCoord coord;
    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    
    int vertexIndex= 0;
    List<Vector3> vertices= new List<Vector3>();
    List<Vector2> uvs= new List<Vector2>();
    List<int> triangles= new List<int>();

    public byte[,,] voxelMap= new byte[VoxelData.ChunkLength, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    World world;
    public bool isVoxelMapPopulated = false;

    // we need to do it like this so that we can generate chunks at a later point in time
    private bool _isActive;
    public bool isActive {
        get { return _isActive; }
        set { 
            _isActive = value;
            if (chunkObject != null)
                chunkObject.SetActive(value); 
        }
    }

    Vector3 position {
        get { return chunkObject.transform.position; }
    }

    public Chunk(ChunkCoord _coord, World _world, bool generateOnLoad) {
        coord = _coord;
        world = _world;
        _isActive = true;

        if (generateOnLoad)
            Init();
    }

    public void Init(){
        chunkObject = new GameObject();

        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshRenderer.material = world.material;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(
            coord.x * VoxelData.ChunkLength, 
            0f, 
            coord.z * VoxelData.ChunkWidth);
        chunkObject.name = coord.x + ", " + coord.z;

        PopulateVoxelMap();
        UpdateChunk();
    }

    void PopulateVoxelMap() {
        for (int x= 0; x< VoxelData.ChunkLength; x++) {
        for (int y= 0; y< VoxelData.ChunkHeight; y++) {
        for (int z= 0; z< VoxelData.ChunkWidth; z++) { 
            voxelMap[x, y, z] = world.GetBlock(new Vector3(x, y, z)+ position);
        }}}
        
        isVoxelMapPopulated = true;
    }

    void UpdateChunk() {
        ClearMeshData();

        for (int x= 0; x< VoxelData.ChunkLength; x++) {
        for (int y= 0; y< VoxelData.ChunkHeight; y++) {
        for (int z= 0; z< VoxelData.ChunkWidth; z++) {
            if (world.blockTypes[voxelMap[x,y,z]].isSolid)
                UpdateMeshData(new Vector3(x, y, z));
        }}}
        
        CreateMesh();
    }

    void ClearMeshData () {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
    }

    public byte GetVoxelFromMap(Vector3 pos) {
        pos-= position;
        return voxelMap[(int)pos.x, (int)pos.y, (int)pos.z];
    }

    bool IsBlockInChunk (int x, int y, int z) {
        if (x< 0 || x> VoxelData.ChunkLength-1 || 
            y< 0 || y> VoxelData.ChunkHeight-1 || 
            z< 0 || z> VoxelData.ChunkWidth-1 ) 
            return false;
        return true;
    }

    public void EditVoxel (Vector3 pos, byte newID) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        x -= Mathf.FloorToInt(chunkObject.transform.position.x);
        z -= Mathf.FloorToInt(chunkObject.transform.position.z);

        voxelMap[x, y, z] = newID;

        // update chunks
        UpdateSurroundingVoxels(x, y, z);
        UpdateChunk();
    }

    void UpdateSurroundingVoxels (int x, int y, int z) {
        Vector3 thisVoxel = new Vector3(x, y, z);
        for (int p = 0; p < 6; p++) {
            Vector3 currentVoxel = thisVoxel + VoxelData.faceChecks[p];

            if (!IsBlockInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z)) {
                world.GetChunkFromVector3(currentVoxel + position).UpdateChunk();
            }
        }
    }

    bool CheckVoxel( Vector3 pos ) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        // If position is outside of this chunk...
        if (!IsBlockInChunk(x, y, z))
            return world.blockTypes[world.GetBlock(pos+ position)].isSolid;
        return world.blockTypes[voxelMap[x, y, z]].isSolid;
    }

    public byte GetVoxelFromGlobalVector3 (Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        x -= Mathf.FloorToInt(chunkObject.transform.position.x);
        z -= Mathf.FloorToInt(chunkObject.transform.position.z);

        return voxelMap[x, y, z];
    }

    void UpdateMeshData( Vector3 pos ) {
        for (int p= 0; p< 6; p++) {
            if (CheckVoxel(pos+ VoxelData.faceChecks[p]))
                continue;

            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,0]]);
            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,1]]);
            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,2]]);
            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,3]]);

            byte blockID = voxelMap[(int)pos.x, (int)pos.y, (int)pos.z];
            AddTexture(world.blockTypes[blockID].GetTextureID(p));

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex+ 1);
            triangles.Add(vertexIndex+ 2);
            triangles.Add(vertexIndex+ 2);
            triangles.Add(vertexIndex+ 1);
            triangles.Add(vertexIndex+ 3);
            vertexIndex+= 4;
        }
    }

    void CreateMesh() {
        Mesh mesh= new Mesh {
            vertices= vertices.ToArray(),
            triangles= triangles.ToArray(),
            uv= uvs.ToArray()
        };
        mesh.RecalculateNormals();
        
        meshFilter.mesh= mesh;
    }

    void AddTexture( int textureId  ) {
        float y= textureId/ VoxelData.TextureAtlasSizeInBlocks;
        float x= textureId- (y* VoxelData.TextureAtlasSizeInBlocks);

        x*= VoxelData.NormalizedBlockTextureSize;
        y*= VoxelData.NormalizedBlockTextureSize;
        // textures go from top left to bottom right. this normalizes our y to the right axis
        y = 1f- y- VoxelData.NormalizedBlockTextureSize;
        
        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y+ VoxelData.NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x+ VoxelData.NormalizedBlockTextureSize, y));
        uvs.Add(new Vector2(x+ VoxelData.NormalizedBlockTextureSize, y+ VoxelData.NormalizedBlockTextureSize));
    }
}
