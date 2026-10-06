using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

public sealed partial class WorldMapControl
{
    private const int TerrainInputStride = ChunkTiles + 2;
    private readonly bool[] _dirtyTerrainTiles = new bool[TerrainInputStride * TerrainInputStride];
    /// <summary>最近一帧地形绘制覆盖的地格数。</summary>
    public int TerrainTilesDrawn { get; private set; }

    private static uint TerrainImageInput(Tile tile)
    {
        return (uint)tile.Terrain | (tile.DroughtTicks > 0 ? 32u : 0u) | ((uint)tile.RoadLevel << 9)
               | (WorldEngine.IsForestTerrain(tile.Terrain) && tile.ResourceAmount < 25 ? 256u : 0u);
    }

    private PixelCanvas UpdateTerrainCanvas(MapChunk chunk, WorldState state, int cx, int cy, int left, int top)
    {
        var first = chunk.TerrainCanvas is null;
        var canvas = chunk.TerrainCanvas ??=
            new PixelCanvas((int)chunk.TerrainBounds.Width, (int)chunk.TerrainBounds.Height);
        Array.Clear(_dirtyTerrainTiles);
        var minX = Math.Max(0, cx - 1);
        var maxX = Math.Min(state.Width, cx + ChunkTiles + 1);
        var minY = Math.Max(0, cy - 1);
        var maxY = Math.Min(state.Height, cy + ChunkTiles + 1);
        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            var slot = (y - cy + 1) * TerrainInputStride + x - cx + 1;
            var input = TerrainImageInput(state.Tiles[y * state.Width + x]);
            if (!first && chunk.TerrainInputs[slot] == input) continue;
            chunk.TerrainInputs[slot] = input;
            // 岸线、山脊、道路及像素边缘依赖邻格；先标记再绘制，避免更新顺序破坏邻接关系。
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (xx >= minX && xx < maxX && yy >= minY && yy < maxY)
                    _dirtyTerrainTiles[(yy - cy + 1) * TerrainInputStride + xx - cx + 1] = true;
            }
        }

        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            if (!_dirtyTerrainTiles[(y - cy + 1) * TerrainInputStride + x - cx + 1]) continue;
            DrawTerrainTile(canvas, state, x, y, (x - cx) * TilePixels + left, (y - cy) * TilePixels + top);
            DrawRoadTile(canvas, state, x, y, (x - cx) * TilePixels + left, (y - cy) * TilePixels + top);
            TerrainTilesDrawn++;
        }

        return canvas;
    }
}
