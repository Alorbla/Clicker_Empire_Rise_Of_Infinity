using System.Globalization;

public static class NumberFormatter
{
    private static readonly string[] Suffixes = { "", "K", "M", "B", "T" };

    public static string Format(int value)
    {
        if (value == 0)
        {
            return "0";
        }

        double abs = value < 0 ? -value : value;
        int suffixIndex = 0;

        while (abs >= 1000d && suffixIndex < Suffixes.Length - 1)
        {
            abs /= 1000d;
            suffixIndex++;
        }

        string number = abs >= 100d ? abs.ToString("0", CultureInfo.InvariantCulture)
            : abs.ToString("0.#", CultureInfo.InvariantCulture);

        string sign = value < 0 ? "-" : "";
        return $"{sign}{number}{Suffixes[suffixIndex]}";
    }
}
