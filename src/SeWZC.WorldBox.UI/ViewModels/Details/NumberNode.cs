using System.Globalization;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>可编辑数值及其范围。</summary>
internal sealed class NumberNode : VisualNode
{
    private decimal? _value;
    public decimal? Value { get => _value; set { if (Set(ref _value, value)) ValueChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? ValueChanged;
    private decimal _minimum = decimal.MinValue;
    public decimal Minimum { get => _minimum; set => Set(ref _minimum, value); }
    private decimal _maximum = decimal.MaxValue;
    public decimal Maximum { get => _maximum; set => Set(ref _maximum, value); }
    private decimal _increment = 1;
    public decimal Increment { get => _increment; set => Set(ref _increment, value); }
    private string? _formatString = null;
    public string? FormatString { get => _formatString; set => Set(ref _formatString, value); }
    private NumberFormatInfo? _numberFormat = CultureInfo.CurrentCulture.NumberFormat;
    public NumberFormatInfo? NumberFormat { get => _numberFormat; set => Set(ref _numberFormat, value); }
}
