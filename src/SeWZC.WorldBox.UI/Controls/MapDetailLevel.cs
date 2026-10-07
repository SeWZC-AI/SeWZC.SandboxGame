namespace SeWZC.WorldBox.UI.Controls;

/// <summary>按地格的屏幕尺寸决定地图细节，避免不可辨识的图案占用绘制预算。</summary>
/// <param name="TileSize">地格边长，单位为控件布局像素。</param>
internal readonly record struct MapDetailLevel(double TileSize)
{
    public bool ResidentHeads => TileSize >= 12;
    public bool ResidentCargo => TileSize >= 16;
    public bool ResidentSprites => TileSize >= 32;
    public bool Ecology => TileSize >= 48;
    public bool ActivityBadges => TileSize >= 64;
    public bool BuildingNames => TileSize >= 64;
    public double MarkerSpacing => TileSize < 8 ? 6 : 3;
}
