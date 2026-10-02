namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly string[][] GivenNames =
    [
        ["艾伦", "伊恩", "罗安", "米拉", "莉娅", "凯恩", "莱恩", "诺拉", "塞琳", "奥文", "菲恩", "艾琳", "卢卡", "西蒙", "薇拉", "卡雅", "洛伊", "梅芙", "埃文", "黛娜", "托恩", "瑞娅", "贝伦", "希尔", "安雅", "尼尔", "塔拉", "索恩", "尤娜", "雷蒙", "芙蕾", "达伦"],
        ["瑟兰", "伊露", "艾薇", "洛希", "菲莉", "诺汐", "星娅", "伊芙", "希露", "奥莉", "莱希", "薇琳", "露弥", "瑟菲", "艾洛", "莉瑟", "银迦", "温蒂", "暮雅", "露恩", "塔蕾", "希薇", "伊诺", "菲洛", "莱雅", "苏缇", "奥希", "阿兰", "米希", "伊莎", "凯露", "奈琳"],
        ["巴林", "杜林", "索林", "布洛", "甘恩", "赫达", "玛格", "洛德", "贝恩", "格林", "芬达", "多恩", "乌尔", "达格", "布琳", "莫拉", "托尔", "瓦恩", "吉姆", "卡德", "弗拉", "哈克", "奥格", "泰克", "博恩", "西格", "厄达", "格瑞", "鲁恩", "维格", "纳尔", "泽拉"],
        ["格罗", "乌戈", "洛卡", "塔格", "玛卡", "祖恩", "布鲁", "杜伽", "阿克", "沙恩", "托迦", "莫格", "哈扎", "库恩", "莱戈", "纳格", "加尔", "乌莎", "卓恩", "拉卡", "提戈", "扎拉", "巴鲁", "索卡", "沃格", "达恩", "卡莎", "欧克", "古拉", "赫戈", "祖拉", "图克"]
    ];
    private static readonly string[] FamilyRoots = ["白", "赤", "银", "金", "青", "灰", "夜", "晨", "星", "月", "霜", "雪", "风", "雨", "云", "海", "山", "河", "松", "橡", "柳", "石", "铁", "铜", "火", "雷", "冬", "夏", "秋", "暮", "远", "长"];
    private static readonly string[] FamilyEnds = ["叶", "枝", "泉", "谷", "丘", "峰", "岸", "湾", "桥", "帆", "歌", "语", "灯", "羽", "翼", "弦", "锤", "砧", "盾", "锋", "牙", "爪", "角", "蹄", "穗", "藤", "铃", "冠", "鹿", "狼", "鹰", "熊"];
    private static readonly string[] PlaceRoots = ["晨曦", "银叶", "铁峰", "赤牙", "河湾", "星湖", "霜原", "长风", "白桦", "暮光", "青岚", "金穗", "雾松", "落霞", "月泉", "苍岩", "远帆", "潮汐", "静溪", "碧潭", "赤枫", "雪岭", "铜炉", "雷鸣", "翠谷", "鹿鸣", "琥珀", "白鹭", "繁花", "松涛", "流萤", "橡木"];
    private static readonly string[] PlaceEnds = ["河", "原", "湖", "谷", "湾", "岭", "林", "港", "桥", "丘", "泉", "台", "岸", "渡", "堡", "坡"];

    private string NewResidentName(int id, RaceKind race)
    {
        // A seed-specific permutation of 32³ combinations, followed by collision checking
        // against player names and archives. IDs stay separate from normal visible names.
        var names = GivenNames[(int)race];
        var code = unchecked((uint)id * 4051u + (uint)State.Seed * 7919u) % 32768;
        var used = State.Residents.Concat(State.ArchivedResidents).Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        for (var attempt = 0; attempt < 32768; attempt++)
        {
            var n = (code + (uint)attempt) % 32768;
            var name = names[n % 32] + "·" + FamilyRoots[n / 32 % 32] + FamilyEnds[n / 1024 % 32];
            if (!used.Contains(name)) return name;
        }
        return names[code % 32] + "·" + id;
    }

    private string NewPlaceName(string suffix)
    {
        var used = State.Nations.Select(n => n.Name).Concat(State.Settlements.Select(t => t.Name)).ToHashSet(StringComparer.Ordinal);
        var code = unchecked((uint)State.NextId * 137u + (uint)State.Seed) % 512;
        for (var i = 0; i < 512; i++)
        {
            var n = (code + (uint)i) % 512;
            var name = PlaceRoots[n % 32] + PlaceEnds[n / 32] + suffix;
            if (!used.Contains(name)) return name;
        }
        return PlaceRoots[code % 32] + State.NextId + suffix;
    }
}
