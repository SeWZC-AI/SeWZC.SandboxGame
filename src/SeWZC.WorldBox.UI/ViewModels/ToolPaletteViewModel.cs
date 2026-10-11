using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>工具分类、分页及绘制和建设参数。</summary>
internal sealed class ToolPaletteViewModel : ViewModelBase
{
    private readonly ToolCategory[] _categories =
        [ToolCategory.Terrain, ToolCategory.Life, ToolCategory.Disaster, ToolCategory.Build];

    private readonly GameSessionViewModel _session;
    private ToolCategory _category = ToolCategory.Terrain;
    private int _page, _pages = 1, _brushIndex, _buildModeIndex, _bridgeDirectionIndex, _bridgeLevelIndex, _townIndex;
    private string _title = "", _hint = "";
    private int[] _towns = [];
    private bool _updatingContext;

    /// <summary>连接工具使用的会话和地图端口。</summary>
    /// <param name="session">当前游戏会话。</param>
    internal ToolPaletteViewModel(GameSessionViewModel session)
    {
        _session = session;
        Slots = Enumerable.Range(0, 8).Select(index => new ToolSlotViewModel(index, SelectTool)).ToArray();
        Categories = _categories.Select((category, index) => new ButtonNode(new UiCommand(_ => SetCategory(category)))
        {
            Content = new[] { "山海", "众生", "天灾", "建设" }[index], AutomationId = "tool-category-" + category.Id,
        }).ToArray();
        PreviousCommand = new UiCommand(_ =>
        {
            _page--;
            SetCategory(_category);
        }, () => _page > 0);
        NextCommand = new UiCommand(_ =>
        {
            _page++;
            SetCategory(_category);
        }, () => _page + 1 < _pages);
    }

    public ShellLayoutViewModel Layout => _session.Layout;
    public ToolSlotViewModel[] Slots { get; }
    public ButtonNode[] Categories { get; }
    public UiCommand PreviousCommand { get; }
    public UiCommand NextCommand { get; }
    public string PageLabel => $"第 {_page + 1} / {_pages} 页";

    public string Title
    {
        get => _title;
        internal set => Set(ref _title, value);
    }

    public string Hint
    {
        get => _hint;
        internal set => Set(ref _hint, value);
    }

    public bool HasBrush => _category.HasBrush;
    public bool IsConstruction => _category.IsConstruction;
    public IReadOnlyList<string> BrushLabels => _category.BrushLabels;
    public string[] TownNames { get; private set; } = [];

    public bool HasTowns => _towns.Length > 0;

    public int TownIndex
    {
        get => _townIndex;
        set
        {
            if (_updatingContext)
                return;
            if (!Set(ref _townIndex, value) || _session.Interaction.IsPickingLocation)
                return;
            if (IsConstruction && value >= 0 && value < _towns.Length)
                _session.Map.SelectedSettlementId = _towns[value];
        }
    }

    public int BrushIndex
    {
        get => _brushIndex;
        set
        {
            if (Set(ref _brushIndex, value))
                _session.Map.ApplyBrush(_category, Math.Max(0, value));
        }
    }

    public int BuildModeIndex
    {
        get => _buildModeIndex;
        set
        {
            if (Set(ref _buildModeIndex, value))
                _session.Map.GiftBuildings = value == 0;
        }
    }

    public int BridgeDirectionIndex
    {
        get => _bridgeDirectionIndex;
        set
        {
            if (!Set(ref _bridgeDirectionIndex, value))
                return;
            _session.Map.ConstructionBridgeDirection = (BridgeDirection)Math.Max(0, value);
            _session.Map.CancelPlacement();
        }
    }

    public int BridgeLevelIndex
    {
        get => _bridgeLevelIndex;
        set
        {
            if (!Set(ref _bridgeLevelIndex, value))
                return;
            _session.Map.ConstructionBridgeLevel = Math.Max(1, value + 1);
            _session.Map.CancelPlacement();
        }
    }

    internal void SetCategory(ToolCategory category)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        _session.Edits.CancelPending();
        _session.Navigation.ClearHistory();
        if (_category != category)
            _page = 0;
        _category = category;
        _session.State.ToolsVisible = true;
        _session.State.InspectorVisible = false;
        _session.Map.ActiveTool = MapTool.Pan;
        _session.Map.CancelPlacement();
        UpdateToolContext();
        _pages = Math.Max(1, (category.Choices.Count + Slots.Length - 1) / Slots.Length);
        _page = Math.Clamp(_page, 0, _pages - 1);
        Notify(nameof(PageLabel));
        PreviousCommand.Refresh();
        NextCommand.Refresh();
        var choices = category.Choices.Skip(_page * Slots.Length).Take(Slots.Length).ToArray();
        for (var i = 0; i < Slots.Length; i++)
            Slots[i].Show(i < choices.Length ? choices[i] : null);
        for (var i = 0; i < Categories.Length; i++)
            Categories[i].Background = Brush.Parse(_categories[i] == category ? "#355347" : "#172632");
        Title = category.Title;
        Hint = category.Hint;
        Notify(nameof(BrushLabels));
        Notify(nameof(HasBrush));
        Notify(nameof(IsConstruction));
        _brushIndex = category.DefaultBrushIndex;
        Notify(nameof(BrushIndex));
        _session.Map.ApplyBrush(category, _brushIndex);
        _session.Layout.Refresh();
    }

    internal void UpdateToolContext()
    {
        _updatingContext = true;
        if (IsConstruction)
        {
            var towns = _session.State.Engine.State.Settlements.OrderBy(town => town.Id).ToArray();
            _towns = towns.Select(town => town.Id).ToArray();
            TownNames = towns.Select(town => town.Name).ToArray();
            var selected = Array.IndexOf(_towns, _session.Map.SelectedSettlementId);
            _townIndex = selected >= 0 ? selected : towns.Length > 0 ? 0 : -1;
            if (_townIndex >= 0)
                _session.Map.SelectedSettlementId = _towns[_townIndex];
        }
        else
        {
            _towns = [];
            TownNames = ["工具分组"];
            _townIndex = 0;
        }

        Notify(nameof(TownNames));
        Notify(nameof(TownIndex));
        Notify(nameof(HasTowns));
        _updatingContext = false;
    }

    internal void SelectTool(MapTool tool)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        _session.Edits.CancelPending();
        _session.Navigation.ClearHistory();
        if (_session.Map.ActiveTool == tool)
        {
            SuspendMapTool(true);
            return;
        }

        _session.Map.ActiveTool = tool;
        _session.Map.CancelPlacement();
        _session.State.InspectorVisible = false;
        Title = Slots.Any(slot => slot.Tool == tool)
            ? _category.Choices.First(choice => choice.Key == tool).Label + "（已启用）"
            : "地图工具已启用";
        foreach (var slot in Slots)
            slot.Select(tool);
        _session.Layout.Refresh();
        _session.SetStatus("工具已启用，再点一次停用；触屏拖动或右键可漫游");
    }

    internal void ToggleTools()
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        _session.Edits.CancelPending();
        _session.Navigation.ClearHistory();
        if (_session.State.ToolsVisible)
        {
            SuspendMapTool(true);
            _session.State.ToolsVisible = false;
        }
        else
        {
            _session.State.ToolsVisible = true;
            _session.State.InspectorVisible = false;
        }

        _session.Layout.Refresh();
    }

    internal void SuspendMapTool(bool endNavigation)
    {
        if (_session.Interaction.IsPickingLocation)
            return;
        _session.Edits.CancelPending();
        if (endNavigation)
            _session.Navigation.ClearHistory();
        _session.Map.CancelPlacement();
        _session.Map.ActiveTool = MapTool.Pan;
        Title = "漫游（点选查看）";
        foreach (var slot in Slots)
            slot.Select(null);
        _session.SetStatus("漫游中，可拖动地图或轻点查看对象");
    }

    internal void EditTerritory(int nation)
    {
        _session.Dialogs.CloseModal();
        SetCategory(ToolCategory.Terrain);
        _session.Map.SelectedNationId = nation;
        _session.Map.ActiveTool = MapTool.Territory;
        Title = "划定疆域";
        Hint = "绘制陆地归属，覆盖聚落会一并转移";
        _session.State.InspectorVisible = false;
        _session.Layout.Refresh();
    }
}
