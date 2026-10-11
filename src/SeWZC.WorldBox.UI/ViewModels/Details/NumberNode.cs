using System.Globalization;

namespace SeWZC.WorldBox.UI.ViewModels.Details;

/// <summary>可编辑数值及其范围。</summary>
internal sealed class NumberNode : VisualNode
{
    private string _formatString = "";
    private decimal _increment = 1;
    private decimal _maximum = decimal.MaxValue;
    private decimal _minimum = decimal.MinValue;
    private NumberFormatInfo? _numberFormat = CultureInfo.CurrentCulture.NumberFormat;
    private decimal? _value;

    public decimal? Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
                ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public decimal Minimum
    {
        get => _minimum;
        set => Set(ref _minimum, value);
    }

    public decimal Maximum
    {
        get => _maximum;
        set => Set(ref _maximum, value);
    }

    public decimal Increment
    {
        get => _increment;
        set => Set(ref _increment, value);
    }

    public string FormatString
    {
        get => _formatString;
        set => Set(ref _formatString, value);
    }

    public NumberFormatInfo? NumberFormat
    {
        get => _numberFormat;
        set => Set(ref _numberFormat, value);
    }

    public event EventHandler? ValueChanged;
}
