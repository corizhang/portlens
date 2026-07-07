using System.Text;

namespace PortLens.Services;

public static class CommandLineNormalizer
{
    /// <summary>
    /// Trims the input and collapses consecutive whitespace characters into a single space.
    /// Returns <c>null</c> for null/empty/whitespace input.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var span = raw.AsSpan().Trim();
        var builder = new StringBuilder(span.Length);
        var previousWasWhitespace = false;

        foreach (var c in span)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
            }
            else
            {
                builder.Append(c);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString();
    }
}
