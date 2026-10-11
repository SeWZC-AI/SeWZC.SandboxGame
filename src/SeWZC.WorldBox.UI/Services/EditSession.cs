namespace SeWZC.WorldBox.UI.Services;

/// <summary>编辑发起时的窗口、导航和地图取点会话。</summary>
/// <param name="Generation">操作窗口的会话版本。</param>
/// <param name="Modal">是否来自操作窗口。</param>
/// <param name="Navigation">详情导航的会话版本。</param>
/// <param name="PickingLocation">当前是否正在地图取点。</param>
internal readonly record struct EditSession(int Generation, bool Modal, int Navigation, bool PickingLocation);
