using Avalonia.Controls;
using Avalonia.Markup.Xaml;
namespace SeWZC.WorldBox.UI.Views;
/// <summary>XAML 模板呈现的详情、列表与编辑字段。</summary>
public sealed partial class DetailView : UserControl
{
    /// <summary>加载详情字段与行的编译模板。</summary>
    public DetailView()
    {
        AvaloniaXamlLoader.Load(this);
        if (Design.IsDesignMode)
            DataContext = ViewModels.PreviewModels.Details();
    }
}
