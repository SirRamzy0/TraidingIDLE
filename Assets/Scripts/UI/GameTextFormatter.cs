using System;
using System.Globalization;
using UnityEngine;

namespace TraidingIDLE.UI
{
    public static class GameTextFormatter
    {
        public static string Format(string format, string fallback, params object[] args)
        {
            var safeFormat = string.IsNullOrWhiteSpace(format) ? fallback : format;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, safeFormat, args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallback, args);
            }
        }

        public static string WholeNumber(double value, string thousandSeparator)
        {
            return Math.Max(0d, value)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", thousandSeparator);
        }

        public static string WholeNumber(long value, string thousandSeparator)
        {
            return Math.Max(0L, value)
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(",", thousandSeparator);
        }

        public static string Percent(double value)
        {
            return Math.Max(0d, value).ToString("0.#", CultureInfo.InvariantCulture);
        }

        public static string CountdownMinutes(double secondsTotal)
        {
            secondsTotal = Math.Max(0, secondsTotal);
            var totalMinutes = (int)(secondsTotal / 60d);
            var seconds = (int)(secondsTotal % 60d);
            return $"{totalMinutes:00}:{seconds:00}";
        }

        public static string CountdownHours(float secondsTotal)
        {
            var total = Mathf.CeilToInt(Mathf.Max(0f, secondsTotal));
            var hours = total / 3600;
            var minutes = total % 3600 / 60;
            var seconds = total % 60;
            return hours > 0 ? $"{hours:00}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
        }
    }
}
