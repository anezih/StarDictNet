namespace System.Text
{
    using System.Globalization;

    public static class DiacriticsMethod
    {
        // https://stackoverflow.com/a/67190157
        public static string RemoveDiacritics(this string text) 
        {
            ReadOnlySpan<char> normalizedString = text.Normalize(NormalizationForm.FormD);
            int i = 0;
            Span<char> span = text.Length < 1000
                ? stackalloc char[text.Length]
                : new char[text.Length];

            foreach (char c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    span[i++] = c;
            }

            return new string(span).Normalize(NormalizationForm.FormC);
        }
    }
}