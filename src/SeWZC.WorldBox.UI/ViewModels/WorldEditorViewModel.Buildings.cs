using Avalonia;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class WorldEditorViewModel
{
    internal void ShowBuildingUpgrade(int id, bool reorient)
    {
        var building = _engine.State.Society.Buildings.FirstOrDefault(b => b.Id == id);
        if (building is null)
            return;
        if (!reorient && building.Level >= 3)
        {
            var completed = ModalPanel("升级建筑", $"{BuildingLabel(building)}已达到最高等级（3 级）。");
            completed.Children.Add(Named(Button("关闭", CloseModal), "upgrade-close"));
            OpenModal(completed);
            return;
        }

        var direction = reorient
            ? (BridgeDirection?)(building.Direction == BridgeDirection.Horizontal
                ? BridgeDirection.Vertical
                : BridgeDirection.Horizontal)
            : null;
        var panel = ModalPanel(reorient ? "改造桥梁方向" : "升级建筑", reorient
            ? $"{WorldEngine.BridgeDirectionName(building.Direction)} 至 {WorldEngine.BridgeDirectionName(direction!.Value)}。施工期间仍沿原方向通行，完工后改向。"
            : $"{BuildingLabel(building)}：{building.Level} 级 至 {building.Level + 1} 级。"
              + (building.Kind == BuildingKind.Bridge ? "施工期间保留原通道，完工后提高离岸上限。" : "升级期间暂停运营，居民到场施工后生效。"));
        panel.Children.Add(Paragraph("施工材料：" + StockLabel(WorldEngine.GetUpgradeCost(building, reorient))));
        panel.Children.Add(Named(
            LiveText(() => "居民施工：" + (_engine.BuildingUpgradeError(id, direction: direction) ?? "材料与条件满足，投入后等待居民到场施工")),
            "upgrade-paid-status"));
        panel.Children.Add(Named(
            LiveText(() => "直接赐予：" + (_engine.BuildingUpgradeError(id, true, direction) ?? "可立即完成，不扣施工材料")),
            "upgrade-gift-status"));
        panel.Children.Add(Named(Button("安排居民施工", () => RunEdit(() =>
        {
            _engine.UpgradeBuilding(id, direction: direction);
            CloseModal();
        }, "项目已开始，等待居民到场施工")), "upgrade-apply"));
        panel.Children.Add(Named(Button("直接赐予完成", () => RunEdit(() =>
        {
            _engine.UpgradeBuilding(id, true, direction);
            CloseModal();
        }, "建筑改造已完成")), "upgrade-gift"));
        OpenModal(panel);
    }

    internal void QuickResidentGoal(int id, AgentGoalKind? kind)
    {
        if (_engine.GetResident(id) is not { Health: > 0 } person)
            return;
        var mind = CreateMindDraft(id);
        var town = _engine.State.Settlements.FirstOrDefault(t => t.Id == person.SettlementId);
        if (kind.HasValue && person.ArmyId != 0)
        {
            SetStatus("军队成员由军令统一调动，请先调整军队归属。");
            return;
        }

        if (kind.HasValue && (town is not null || kind == AgentGoalKind.Rest))
        {
            var dwelling = kind == AgentGoalKind.Rest ? _engine.GetResidentHome(id) : null;
            mind = mind with
            {
                DaytimeGoal = null,
                Goal = new AgentGoal
                {
                    Kind = kind.Value,
                    TargetX = kind == AgentGoalKind.Rest ? dwelling?.X ?? person.X : town!.X,
                    TargetY = kind == AgentGoalKind.Rest ? dwelling?.Y ?? person.Y : town!.Y,
                    TargetEntityId = dwelling?.Id ?? 0,
                    TargetSettlementId = person.SettlementId,
                    StartedTick = _engine.State.Tick,
                    ReviewTick = _engine.State.Tick + 48,
                    PlayerDirected = true,
                    Reason = kind == AgentGoalKind.Rest
                        ? dwelling is not null ? "玩家安排返回住宅休息" : "玩家安排在当地露宿休息"
                        : "玩家安排返回家园交付物资",
                },
            };
        }
        else
        {
            mind = mind with { Goal = mind.Goal with { PlayerDirected = false } };
            mind = mind with { NextThinkTick = _engine.State.Tick };
        }

        RunEdit(() => _engine.EditResident(id, new ResidentEdit { Agent = mind }),
            kind.HasValue ? "已安排任务，继续模拟后执行" : "已恢复自主行动");
    }

    internal void ShowLandProject(int x, int y)
    {
        var towns = _engine.State.Settlements.OrderBy(t => Math.Abs(t.X - x) + Math.Abs(t.Y - y)).ToArray();
        if (towns.Length == 0)
        {
            SetStatus("需要先有聚落与居民。");
            return;
        }

        var terrain = _engine.State.Tiles[y * _engine.State.Width + x].Terrain;
        var kind = terrain == TerrainType.Mountain ? BuildingKind.MountainPass
            : terrain is TerrainType.Water or TerrainType.River or TerrainType.Lake or TerrainType.Stream
                or TerrainType.LargeRiver ? BuildingKind.Bridge : BuildingKind.Farm;
        var panel = ModalPanel("安排居民改造地块", $"{WorldEngine.BuildingName(kind)}\n投入材料后，由居民到场施工；桥梁和山路完工后才可通行。");
        var town = ObjectField(panel, "负责聚落", towns.Select(t => (t.Id, t.Name)), towns[0].Id, "land-town");
        var bridgeOptions = new StackNode { IsVisible = kind == BuildingKind.Bridge };
        panel.Children.Add(bridgeOptions);
        var direction = EnumField(bridgeOptions, "桥梁方向", BridgeDirection.Horizontal, WorldEngine.BridgeDirectionName,
            "land-bridge-direction");
        direction.IsVisible = kind == BuildingKind.Bridge;
        var level = ObjectField(bridgeOptions, "桥梁等级",
            [(1, "1 级：离岸 2 格"), (2, "2 级：离岸 4 格"), (3, "3 级：离岸 6 格")], 1, "land-bridge-level");
        level.IsVisible = kind == BuildingKind.Bridge;
        panel.Children.Add(LiveText(() =>
            "材料：" + StockLabel(WorldEngine.FacilityCost(kind, kind == BuildingKind.Bridge ? Integer(level) : 1))));
        panel.Children.Add(Named(Button("开始居民施工", () => RunEdit(() =>
        {
            _engine.BuildFacility(Integer(town), kind, x, y,
                kind == BuildingKind.Bridge ? (BridgeDirection?)direction.SelectedItem : null,
                kind == BuildingKind.Bridge ? Integer(level) : 1);
            CloseModal();
        }, "改造已立项，继续模拟后居民会到场施工")), "land-apply"));
        OpenModal(panel);
    }
}
