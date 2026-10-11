using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SeWZC.WorldBox.UI.ViewModels;

/// <summary>界面区域模型的属性变更通知。</summary>
internal abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>更新发生变化的绑定属性。</summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    /// <param name="field">属性的存储字段。</param>
    /// <param name="value">新值。</param>
    /// <param name="name">属性名称。</param>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Notify(name);
        return true;
    }

    /// <summary>通知绑定重新读取属性。</summary>
    /// <param name="name">属性名称。</param>
    protected void Notify([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
