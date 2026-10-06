namespace SeWZC.WorldBox.Core;

/// <summary>国家对科技与魔法发展路线的规划偏好。</summary>
public enum DevelopmentFocus
{
    /// <summary>按当地文化选择。</summary>
    Automatic,
    /// <summary>科技发展。</summary>
    Technology,
    /// <summary>法术传承。</summary>
    MagicPractice,
    /// <summary>魔法工艺。</summary>
    ArcaneIndustry,
    /// <summary>兼修科技与魔法。</summary>
    Integrated,
}
