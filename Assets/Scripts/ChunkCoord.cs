using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkCoord {
    public int x;
    public int z;

    public ChunkCoord () {
        x = 0;
        z = 0;
    }
    public ChunkCoord(int _x, int _z) {
        x = _x;
        z = _z;
    }
    public ChunkCoord(Vector3 pos) {
        x = Mathf.FloorToInt(pos.x)/ VoxelData.ChunkLength;
        z = Mathf.FloorToInt(pos.z)/ VoxelData.ChunkWidth;
    }

    public bool Equals(ChunkCoord other) {
        if (other == null) 
            return false;
        if (other.x == x && other.z == z) 
            return true;
        return false;
    }
}
