using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Controls;
using SeWZC.WorldBox.UI.ViewModels.Details;

namespace SeWZC.WorldBox.UI.ViewModels;

internal sealed partial class WorldEditorViewModel
{
    internal void ShowStorage()
    {
        var panel = ModalPanel("保存你的世界", "存档保存在这台设备的浏览器中。导出文件可以备份或带到另一台设备。");
        panel.Children.Add(Button("保存到本机", async () =>
        {
            CloseModal();
            await SaveAsync(true);
        }));
        panel.Children.Add(Button("读取本机存档", async () =>
        {
            if (!CanSubmitEdit())
                return;
            var source = _engine;
            var generation = _requests.ModalGeneration();
            var storage = _requests.Storage;

            bool Current()
            {
                return ReferenceEquals(source, _engine) && generation == _requests.ModalGeneration() && _requests.ModalVisible();
            }

            try
            {
                if (!storage.IsAvailable)
                    return;
                string? json;
                json = await storage.LoadAsync(Current);

                if (!Current())
                    return;
                if (json is null)
                {
                    SetStatus("还没有本机存档");
                    return;
                }

                var candidate = WorldEngine.ImportJson(json);
                await SubmitEditAsync(() =>
                {
                    ReplaceWorld(candidate);
                    SetStatus("已读取本机存档");
                }, true);
            }
            catch (Exception ex)
            {
                if (Current())
                    SetStatus($"读取失败：{FriendlyError(ex)}");
            }
        }));
        panel.Children.Add(Button("导出世界文件", async () => await ExportWorldAsync()));
        panel.Children.Add(Button("导入世界文件", async () =>
        {
            if (!CanSubmitEdit())
                return;
            var source = _engine;
            var generation = _requests.ModalGeneration();
            var storage = _requests.Storage;

            bool Current()
            {
                return ReferenceEquals(source, _engine) && generation == _requests.ModalGeneration() && _requests.ModalVisible();
            }

            try
            {
                if (!storage.IsAvailable)
                    return;
                var json = await storage.ImportAsync();
                if (json is null || !Current())
                    return;
                var candidate = WorldEngine.ImportJson(json);
                await SubmitEditAsync(() =>
                {
                    ReplaceWorld(candidate);
                    SetStatus("导入成功，时间已暂停");
                }, true);
            }
            catch (Exception ex)
            {
                if (Current())
                    SetStatus($"导入失败，当前世界未改变：{FriendlyError(ex)}");
            }
        }));
        panel.Children.Add(Button("撤销本轮编辑", _viewModel.UndoCommand));
        OpenModal(panel);
    }

    internal void ShowNewWorld()
    {
        var panel = ModalPanel("让一个新世界诞生", "相同的种子生成相同的山海。当前世界会暂存为本次会话的恢复点。");
        panel.Children.Add(Text("世界种子", 12, Muted));
        var seed = Named(
            new ViewModels.Details.NumberNode
            {
                Minimum = int.MinValue,
                Maximum = int.MaxValue,
                Increment = 1,
                Value = Random.Shared.Next(10000, 99999),
                FormatString = "0",
            }, "world-seed");
        panel.Children.Add(seed);
        panel.Children.Add(Text("世界大小", 12, Muted));
        var size = Named(
            new ViewModels.Details.ChoiceNode
            {
                ItemsSource = new[] { "小型世界（128 × 128）", "中型世界（256 × 256）" },
                SelectedIndex = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            }, "world-size");
        panel.Children.Add(size);
        var life = Named(new ViewModels.Details.CheckNode { Content = Text("播下四个种族，立即开始观察", 12), IsChecked = true }, "world-initial-life");
        panel.Children.Add(life);
        var create = Button("创造世界", async () =>
        {
            if (!CanSubmitEdit())
                return;
            if (seed.Value is not { } seedNumber || seedNumber != decimal.Truncate(seedNumber))
            {
                SetStatus("种子应为一个有效整数");
                return;
            }

            var seedValue = Integer(seed);
            var dimension = size.SelectedIndex == 0 ? 128 : 256;
            var initialLife = life.IsChecked == true;
            await SubmitEditAsync(() =>
            {
                ReplaceWorld(WorldEngine.Create(seedValue, dimension, dimension, initialLife));
                _inspectorMode = "overview";
                SetStatus("新世界已诞生，点击继续让时间开始流动");
            }, true);
        });
        create.Background = Mint;
        create.Foreground = Ink;
        panel.Children.Add(create);
        OpenModal(panel);
    }

    internal void ShowNationEditor(int nationId)
    {
        var nation = _engine.State.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation is null)
            return;
        var panel = ModalPanel("改写文明的方向", "编辑国家名称、库存与外交关系。变更将在暂停的世界中生效。");
        var name = Named(new ViewModels.Details.TextInputNode { Text = nation.Name, MaxLength = 40 }, "nation-name");
        panel.Children.Add(name);
        panel.Children.Add(Text("旗色", 12, Muted));
        var color = nation.ColorArgb;
        var palette = new ViewModels.Details.StackNode { Orientation = Orientation.Horizontal, Spacing = 6 };
        var swatches = new List<(uint Color, ViewModels.Details.ButtonNode Button)>();
        foreach (var argb in new[]
                 {
                     0xFFE7AD62, 0xFF63CCA7, 0xFF8C9DEB, 0xFFE27A7C, 0xFFDFC16E, 0xFFB593DB, 0xFF74BBDC, 0xFFD294C8,
                 })
        {
            var swatch = new ViewModels.Details.ButtonNode
            {
                Width = 34,
                Height = 34,
                MinHeight = 34,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromUInt32(argb)),
                BorderThickness = new Thickness(2),
                BorderBrush = argb == color ? Brushes.White : Brushes.Transparent,
            };
            swatch.Click += (_, _) =>
            {
                color = argb;
                foreach (var entry in swatches)
                    entry.Button.BorderBrush = entry.Color == color ? Brushes.White : Brushes.Transparent;
            };
            swatches.Add((argb, swatch));
            palette.Children.Add(swatch);
        }

        panel.Children.Add(palette);
        panel.Children.Add(Text("古代发展水平（影响生产效率）", 12, Muted));
        var technology =
            Named(
                new ViewModels.Details.ChoiceNode
                {
                    ItemsSource = new[] { "1  部落", "2  定居", "3  农业", "4  冶炼", "5  城邦" },
                    SelectedIndex = Math.Clamp(nation.Technology - 1, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "nation-technology");
        panel.Children.Add(technology);
        panel.Children.Add(Text("自主发展方向", 12, Muted));
        var focus = Named(
            new ViewModels.Details.ChoiceNode
            {
                ItemsSource = Enum.GetValues<DevelopmentFocus>().Select(WorldEngine.DevelopmentFocusName).ToArray(),
                SelectedIndex = (int)nation.DevelopmentFocus,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            }, "nation-development-focus");
        panel.Children.Add(focus);
        panel.Children.Add(Paragraph("科技优先工业与能源；法术传承依靠施法者与训练，不要求魔晶设施。魔法工艺与兼修路线才会自主建设魔晶生产链。"));
        var fields = new List<ViewModels.Details.NumberNode>();
        foreach (var kind in ResourceStock.Kinds)
        {
            var value = nation.Resources.Get(kind);
            var input = Field(panel, ResourceStock.Name(kind), value, "nation-" + kind.ToString().ToLowerInvariant(),
                Math.Max(1_000_000, value));
            fields.Add(input);
        }

        var others = _engine.State.Nations.Where(n => n.Id != nationId).ToList();
        var other = Named(
            new ViewModels.Details.ChoiceNode
            {
                ItemsSource = others.Select(n => n.Name).ToArray(),
                SelectedIndex = others.Count > 0 ? 0 : -1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            }, "nation-diplomacy-target");
        var diplomacy =
            Named(
                new ViewModels.Details.ChoiceNode
                {
                    ItemsSource = new[] { "保持现有关系", "和平", "结盟", "宣战" },
                    SelectedIndex = 0,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                }, "nation-diplomacy");
        if (others.Count > 0)
        {
            panel.Children.Add(Text("与另一个国家的关系", 12, Muted));
            panel.Children.Add(other);
            panel.Children.Add(diplomacy);
        }

        panel.Children.Add(Button("应用变更", async () =>
        {
            if (!CanSubmitEdit())
                return;
            var values = new double?[ResourceStock.Kinds.Count];
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Any(char.IsControl))
            {
                SetStatus("请输入不含控制字符的国家名称");
                return;
            }

            for (var i = 0; i < fields.Count; i++)
            {
                if (fields[i].Value is null)
                {
                    SetStatus("请填写资源数量");
                    return;
                }

                var value = Number(fields[i]);
                if (value == (double)fields[i].Tag!)
                    continue;
                if (value is < 0 or > 1_000_000)
                {
                    SetStatus("修改后的资源须在 0 到 1,000,000 之间");
                    return;
                }

                values[i] = value;
            }

            var nextName = name.Text.Trim();
            var nextFocus = (DevelopmentFocus)focus.SelectedIndex;
            var nextColor = color;
            var nextTechnology = technology.SelectedIndex + 1;
            var diplomacyChoice = diplomacy.SelectedIndex;
            var otherId = other.SelectedIndex >= 0 ? others[other.SelectedIndex].Id : 0;
            await SubmitEditAsync(() =>
            {
                _engine.RenameNation(nationId, nextName);
                _engine.SetNationResources(nationId, values[0], values[1], values[2], values[3], values[4], values[5],
                    values[6], values[7], values[8], values[9], values[10], values[11], values[12]);
                _engine.SetDevelopmentFocus(nationId, nextFocus);
                _engine.SetNationColor(nationId, nextColor);
                _engine.SetNationTechnology(nationId, nextTechnology);
                if (diplomacyChoice > 0 && otherId > 0)
                {
                    _engine.SetDiplomacy(nationId, otherId,
                        diplomacyChoice switch
                        {
                            2 => DiplomaticStatus.Allied,
                            3 => DiplomaticStatus.War,
                            _ => DiplomaticStatus.Neutral,
                        });
                }

                CloseModal();
                _map.RefreshWorld();
                RefreshUi(true);
                SetStatus("国家已更新");
            });
        }));
        panel.Children.Add(new ViewModels.Details.BorderNode { Height = 1, Background = Line });
        panel.Children.Add(Button("绘制这个国家的领土", async () =>
        {
            await SubmitEditAsync(() =>
            {
                _requests.TerritoryEdit(nationId);
                SetStatus("领土编辑中，覆盖聚落会转移其居民与库存；支持撤销");
            });
        }));
        panel.Children.Add(Text("向首都添加居民（每次 12 人）", 12, Muted));
        var people = new ViewModels.Details.StackNode { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var race in Enum.GetValues<RaceKind>())
            people.Children.Add(Button(RaceName(race), async () =>
            {
                if (!CanSubmitEdit())
                    return;
                var capital = _engine.State.Settlements.First(s => s.Id == nation.CapitalId);
                await SubmitEditAsync(() =>
                {
                    _engine.SpawnResidents(capital.X, capital.Y, race);
                    CloseModal();
                    _map.RefreshWorld();
                    RefreshUi(true);
                    SetStatus("新居民已加入这个国家");
                });
            }));
        panel.Children.Add(people);
        var towns = _engine.State.Settlements.Where(s => s.NationId == nationId).ToList();
        if (towns.Count >= 2)
        {
            panel.Children.Add(Text("选择一处聚落独立建国", 12, Muted));
            var townPicker = new ViewModels.Details.ChoiceNode
            {
                ItemsSource = towns.Select(t => t.Name).ToArray(),
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            panel.Children.Add(townPicker);
            panel.Children.Add(Button("让聚落独立", async () =>
            {
                if (!CanSubmitEdit())
                    return;
                try
                {
                    var town = towns[townPicker.SelectedIndex];
                    var newName = town.Name.Length > 36 ? town.Name[..36] : town.Name;
                    await SubmitEditAsync(() =>
                    {
                        _selectedNationId = _engine.SplitSettlement(town.Id, newName + "国");
                        _inspectorMode = "nation";
                        CloseModal();
                        _map.RefreshWorld();
                        RefreshUi(true);
                        SetStatus("一个新的国家诞生了");
                    });
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    SetStatus(FriendlyError(ex));
                }
            }));
        }
        else
            panel.Children.Add(Paragraph("拥有两处及以上聚落后，可以拆分国家。"));

        OpenModal(panel);
    }

}
