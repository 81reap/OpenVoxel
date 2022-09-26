using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour {
    public int seed;
    public BiomeAttributes biome;

    public Transform player;
    public Vector3 spawn;

    public Material material;
    public BlockType[] blockTypes;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    public ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    private bool isCreatingChunks;

    public GameObject debugScreen;

    private void Start() {
        Random.InitState(seed);

        spawn = new Vector3(VoxelData.WorldSizeInBlocks / 2f, VoxelData.ChunkHeight- 50f, VoxelData.WorldSizeInBlocks / 2f);
        GenerateWorld();
        playerChunkCoord = GetChunkCoordFromVector3(player.position);
        CheckViewDistance();
    }

    private void Update() {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        if (!GetChunkCoordFromVector3(player.transform.position).Equals(playerLastChunkCoord))
            CheckViewDistance();
        if (chunksToCreate.Count > 0 && !isCreatingChunks)
            StartCoroutine("CreateChunks");
        if (Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);
    }

    ChunkCoord GetChunkCoordFromVector3 (Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        return new ChunkCoord(x, z);
    }

    IEnumerator CreateChunks () {
        isCreatingChunks = true;
        while (chunksToCreate.Count > 0) {
            chunks[chunksToCreate[0].x, chunksToCreate[0].z].Init();
            chunksToCreate.RemoveAt(0);
            yield return null;
        }
        isCreatingChunks = false;   
    }

    void GenerateWorld() {
        int x = Mathf.Max(0, VoxelData.WorldSizeInChunks/ 2- VoxelData.ViewDistanceInChunks);
        for (; x< Mathf.Min(VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks/ 2+ VoxelData.ViewDistanceInChunks); x++) {
            int z = Mathf.Max(0, VoxelData.WorldSizeInChunks/ 2- VoxelData.ViewDistanceInChunks);
            for (; z< Mathf.Min(VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks/ 2+ VoxelData.ViewDistanceInChunks); z++) {
                chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, true);
                activeChunks.Add(new ChunkCoord(x, z));
            }
        }

        player.position = spawn;
    }

    // our xyz is the bottom left corner of our chunk
    public bool CheckForBlock (Vector3 pos) {
        ChunkCoord chunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(chunk.x, chunk.z) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[chunk.x, chunk.z] != null && chunks[chunk.x, chunk.z].isVoxelMapPopulated)
            return blockTypes[chunks[chunk.x, chunk.z].GetVoxelFromGlobalVector3(pos)].isSolid;

        return blockTypes[GetBlock(pos)].isSolid;
    }

    void CheckViewDistance() {
        ChunkCoord coord= GetChunkCoordFromVector3(player.position);
        playerLastChunkCoord = playerChunkCoord;
        List<ChunkCoord> previouslyActiveChunks= new List<ChunkCoord>(activeChunks);

        // Loop through all chunks currently within view distance of the player.
        for (int x= coord.x- VoxelData.ViewDistanceInChunks; x< coord.x+ VoxelData.ViewDistanceInChunks; x++) {
            for (int z= coord.z- VoxelData.ViewDistanceInChunks; z< coord.z + VoxelData.ViewDistanceInChunks; z++) {

                // If the current chunk is in the world...
                if (IsChunkInWorld(x, z)) {
                    // Check if it active, if not, activate it.
                    if (chunks[x, z] == null) {
                        chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, false);
                        chunksToCreate.Add(new ChunkCoord(x, z));
                    } else if (!chunks[x, z].isActive) {
                        chunks[x, z].isActive= true;
                    }

                    activeChunks.Add(new ChunkCoord(x, z));
                }

                // Check through previously active chunks to see if this chunk is there. If it is, remove it from the list.
                for (int i= 0; i< previouslyActiveChunks.Count; i++) {
                    if (previouslyActiveChunks[i].Equals(new ChunkCoord(x, z)))
                        previouslyActiveChunks.RemoveAt(i);
                }
            }
        }

        // Any chunks left in the previousActiveChunks list are no longer in the player's view distance, so loop through and disable them.
        foreach (ChunkCoord c in previouslyActiveChunks)
            chunks[c.x, c.z].isActive= false;
    }

    bool IsChunkInWorld(int x, int z) {
        if (x> 0 && x< VoxelData.WorldSizeInChunks-1 && 
            z> 0 && z< VoxelData.WorldSizeInChunks-1)
            return true;
        return false;
    }

    bool IsBlockInWorld (Vector3 pos) {
        if (pos.x>= 0 && pos.x< VoxelData.WorldSizeInBlocks && 
            pos.y>= 0 && pos.y< VoxelData.ChunkHeight && 
            pos.z>= 0 && pos.z< VoxelData.WorldSizeInBlocks)
            return true;
        return false;
    }

    public byte GetBlock(Vector3 pos) {
        // -!- immutable pass -!-
        // If outside world, return air.
        if (!IsBlockInWorld(pos))
            return 0;
        
        // bottom block of the chunk, return bedrock
        int yAbs = Mathf.FloorToInt(pos.y);
        if (yAbs == 0)
            return 1;

        // -!- basic terrain pass -!-
        int terrainHeight = Mathf.FloorToInt(biome.terrainHeight* Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale)) + biome.solidGroundHeight;
        byte voxelValue = 0;
        if (yAbs == terrainHeight)
            voxelValue= 3; // grass
        else if (yAbs< terrainHeight && yAbs> terrainHeight- 4)
            voxelValue= 4; // dirt
        else if (yAbs> terrainHeight)
            return 0; // air
        else
            voxelValue= 2; // stone

        // -!- second pass -!-
        if (voxelValue == 2) {
            foreach (Lode lode in biome.lodes) {
                if (yAbs> lode.minHeight && yAbs< lode.maxHeight)
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                        voxelValue = lode.blockID;
            }
        }

        return voxelValue;
    }
}

[System.Serializable]
public class BlockType {
    public string blockName;
    public bool isSolid;

    [Header("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    // Back, Front, Top, Bottom, Left, Right
    public int GetTextureID (int faceIndex) {
        switch (faceIndex) {
            case 0:
                return backFaceTexture;
            case 1:
                return frontFaceTexture;
            case 2:
                return topFaceTexture;
            case 3:
                return bottomFaceTexture;
            case 4:
                return leftFaceTexture;
            case 5:
                return rightFaceTexture;
            default:
                Debug.Log("Error in GetTextureID; invalid face index");
                return 0;
        }
    }
}
