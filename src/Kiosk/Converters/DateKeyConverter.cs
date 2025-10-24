using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Kiosk.Converters;

public sealed class DateKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;

        // CheckIn -> CheckOut 순으로 날짜 선택
        DateTime? dt = GetNullableDate(value, "CheckIn") ?? GetNullableDate(value, "CheckOut");
        if (dt is null) return string.Empty; // 날짜 없는 항목은 같은(빈) 그룹으로

        var ci = culture?.Name == "ko-KR" ? culture : new CultureInfo("ko-KR");
        return dt.Value.ToString("yyyy-MM-dd (ddd)", ci);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static DateTime? GetNullableDate(object obj, string propName)
    {
        var p = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (p == null) return null;

        var v = p.GetValue(obj);
        if (v == null) return null;

        if (v is DateTime dt) return dt;
        return null;
    }
}