using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// this class currently works in the same order
// back, front, top, bottom, left, right
public static class VoxelData {

    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkLength = 16;
    public static readonly int ChunkHeight = 128;

    // Lighting Values
    public static float minLightLevel = 0.1f;
    public static float maxLightLevel = 0.9f;
    public static float lightFalloff = 0.08f;

	public static readonly int WorldSizeInChunks = 50;//100;
    public static int WorldSizeInVoxels {
        get { return WorldSizeInChunks* ChunkWidth; }
    }

    public static readonly int TextureAtlasSizeInBlocks = 16;
    public static float NormalizedBlockTextureSize {
        get { return 1f/ (float)TextureAtlasSizeInBlocks; }
    }

    // mathematically better but doesnt work with unity lighting
    // to fix that add offsets of 0.05
    public static readonly Vector3[] voxelVerts = {
        new Vector3(0.0f, 0.0f, 0.0f),
		new Vector3(1.0f, 0.0f, 0.0f),
		new Vector3(1.0f, 1.0f, 0.0f),
		new Vector3(0.0f, 1.0f, 0.0f),
		new Vector3(0.0f, 0.0f, 1.0f),
		new Vector3(1.0f, 0.0f, 1.0f),
		new Vector3(1.0f, 1.0f, 1.0f),
		new Vector3(0.0f, 1.0f, 1.0f)
    };

    public static readonly Vector2[] voxelUvs = {
        new Vector2 (0.0f, 0.0f),
		new Vector2 (0.0f, 1.0f),
		new Vector2 (1.0f, 0.0f),
		new Vector2 (1.0f, 1.0f)
    };

    public static readonly Vector3[] faceChecks = {
        new Vector3(0.0f, 0.0f, -1.0f),
		new Vector3(0.0f, 0.0f, 1.0f),
		new Vector3(0.0f, 1.0f, 0.0f),
		new Vector3(0.0f, -1.0f, 0.0f),
		new Vector3(-1.0f, 0.0f, 0.0f),
		new Vector3(1.0f, 0.0f, 0.0f)
    };

    public static readonly int[,] voxelTris = {
        // 0 1 2 2 1 3
		{0, 3, 1, 2}, // Back Face
		{5, 6, 4, 7}, // Front Face
		{3, 7, 2, 6}, // Top Face
		{1, 5, 0, 4}, // Bottom Face
		{4, 7, 0, 3}, // Left Face
		{1, 2, 5, 6}  // Right Face
    };
}
