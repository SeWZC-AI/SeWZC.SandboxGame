using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>详情和表单的Text展示状态。</summary>
internal sealed class TextNode : VisualNode
{
    private string? _text = "";
    public string? Text { get => _text; set => Set(ref _text, value); }
    private TextWrapping _textWrapping = TextWrapping.NoWrap;
    public TextWrapping TextWrapping { get => _textWrapping; set => Set(ref _textWrapping, value); }
    private double _lineHeight = double.NaN;
    public double LineHeight { get => _lineHeight; set => Set(ref _lineHeight, value); }
}
