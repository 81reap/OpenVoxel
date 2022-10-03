using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class Chunk {
    ChunkCoord coord;
    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    
    int vertexIndex= 0;
    List<Vector3> vertices= new List<Vector3>();
    List<Vector2> uvs= new List<Vector2>();
    List<Color> colours= new List<Color>();
    List<int> triangles= new List<int>();
    List<int> transparentTriangles = new List<int>();
    Material[] materials = new Material[2];

    public Vector3 position;

    public byte[,,] voxelMap= new byte[VoxelData.ChunkLength, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

    public Queue<VoxelMod> modifications = new Queue<VoxelMod>();

    World world;
    private bool isVoxelMapPopulated = false;
    private bool threadLocked = false;

    /*
    notes about threading
     1. unity does not allow you to draw game objects on background threads
     2. threading errors and logs will not show up in the console
    */

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

    public bool isEditable {
        get {
            if (!isVoxelMapPopulated || threadLocked)
                return false;
            return true;
        }
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
        
        //materials[0] = world.material;
        //materials[1] = world.transparentMaterial;
        meshRenderer.material = world.material;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(
            coord.x * VoxelData.ChunkLength, 
            0f, 
            coord.z * VoxelData.ChunkWidth);
        chunkObject.name = coord.x + ", " + coord.z;
        position = chunkObject.transform.position;

        Thread t = new Thread(new ThreadStart(PopulateVoxelMap));
        t.Start();
    }

    void PopulateVoxelMap() {
        for (int x= 0; x< VoxelData.ChunkLength; x++) {
        for (int y= 0; y< VoxelData.ChunkHeight; y++) {
        for (int z= 0; z< VoxelData.ChunkWidth; z++) { 
            voxelMap[x, y, z] = world.GetBlock(new Vector3(x, y, z)+ position);
        }}}
        
        _updateChunk();
        isVoxelMapPopulated = true;
    }

    public void UpdateChunk(){
        Thread t = new Thread(new ThreadStart(_updateChunk));
        t.Start();
    }

    private void _updateChunk() {
        threadLocked = true;

        while (modifications.Count > 0) {
            VoxelMod v = modifications.Dequeue();
            Vector3 pos = v.position -= position;
            voxelMap[(int)pos.x, (int)pos.y, (int)pos.z] = v.id;
        }

        ClearMeshData();

        for (int x= 0; x< VoxelData.ChunkLength; x++) {
        for (int y= 0; y< VoxelData.ChunkHeight; y++) {
        for (int z= 0; z< VoxelData.ChunkWidth; z++) {
            if (world.blockTypes[voxelMap[x,y,z]].isSolid)
                UpdateMeshData(new Vector3(x, y, z));
        }}}
        
        lock(world.chunksToDraw) {
            world.chunksToDraw.Enqueue(this);
        }

        threadLocked = false;
    }

    void ClearMeshData () {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
        colours.Clear();
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
            return world.CheckIfVoxelTransparent(pos+ position);
        return world.blockTypes[voxelMap[x, y, z]].isTransparent;
    }

    public byte GetVoxelFromGlobalVector3 (Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        x -= Mathf.FloorToInt(position.x);
        z -= Mathf.FloorToInt(position.z);

        return voxelMap[x, y, z];
    }

    void UpdateMeshData( Vector3 pos ) {
        byte blockID = voxelMap[(int)pos.x, (int)pos.y, (int)pos.z];
        bool isTransparent = world.blockTypes[blockID].isTransparent;
        
        for (int p= 0; p< 6; p++) {
            if (!CheckVoxel(pos+ VoxelData.faceChecks[p]))
                continue;

            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,0]]);
            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,1]]);
            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,2]]);
            vertices.Add(pos+ VoxelData.voxelVerts[VoxelData.voxelTris[p,3]]);

            AddTexture(world.blockTypes[blockID].GetTextureID(p));

            float lightLevel = 0f;
            int yPos = (int) pos.y + 1;
            bool inShade = false;
            while (yPos < VoxelData.ChunkHeight) {
                if (voxelMap[(int)pos.x, yPos, (int)pos.z] != 0) {
                    inShade = true;
                    break;
                }

                yPos++;
            }

            if (inShade)
                lightLevel = 0.4f;

            colours.Add(new Color(0, 0, 0,lightLevel));
            colours.Add(new Color(0, 0, 0,lightLevel));
            colours.Add(new Color(0, 0, 0,lightLevel));
            colours.Add(new Color(0, 0, 0,lightLevel));

            //if (!isTransparent) {
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex+ 1);
                triangles.Add(vertexIndex+ 2);
                triangles.Add(vertexIndex+ 2);
                triangles.Add(vertexIndex+ 1);
                triangles.Add(vertexIndex+ 3);
            /*} else {
                transparentTriangles.Add(vertexIndex);
                transparentTriangles.Add(vertexIndex+ 1);
                transparentTriangles.Add(vertexIndex+ 2);
                transparentTriangles.Add(vertexIndex+ 2);
                transparentTriangles.Add(vertexIndex+ 1);
                transparentTriangles.Add(vertexIndex+ 3);
            }*/
            
            vertexIndex+= 4;
        }
    }

    public void CreateMesh() {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();

        //mesh.subMeshCount = 2;
        //mesh.SetTriangles(triangles.ToArray(), 0);
        //mesh.SetTriangles(transparentTriangles.ToArray(), 1);
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colours.ToArray();

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
