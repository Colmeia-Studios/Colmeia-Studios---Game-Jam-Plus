using System;
using System.Globalization;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public static class OrderNumberFormatter
    {
        /// <summary>Formats display only; the order ID used by gameplay remains unchanged.</summary>
        public static string Format(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return "#---";
            string value = orderId.Trim();
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                return $"#{number:000}";
            string[] segments = value.Split('-');
            if (segments.Length >= 3 && segments[0].Length > 1 && segments[0][0] == 'D' &&
                int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return $"#{number:000}";
            return $"#{value}";
        }
    }
}
