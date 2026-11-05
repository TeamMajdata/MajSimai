using System;
using System.Globalization;
using System.Text;

namespace MajSimai.Parsing;

/// <summary>
/// Writes a <see cref="SimaiFile"/> back to simai text. Charts are written from <see cref="SimaiChart.Fumen"/>; their
/// timing points are not used.
/// </summary>
internal static class SimaiDeparser
{
    /// <summary>
    /// Writes a simai file as text. Lines end with <see cref="Environment.NewLine"/>.
    /// </summary>
    /// <param name="simaiFile">The file to write.</param>
    /// <returns>The simai text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="simaiFile"/> is <see langword="null"/>.</exception>
    public static string Deparse(SimaiFile simaiFile)
    {
        if (simaiFile is null)
        {
            throw new ArgumentNullException(nameof(simaiFile));
        }
        var builder = new StringBuilder();
        builder.Append("&title=").AppendLine(simaiFile.Title)
               .Append("&artist=").AppendLine(simaiFile.Artist)
               .Append("&des=").AppendLine(simaiFile.FinalDesigner)
               .Append("&first=").AppendLine(simaiFile.Offset.ToString(CultureInfo.InvariantCulture));
        var charts = simaiFile.Charts;
        for (var i = 0; i < charts.Length; i++)
        {
            var chart = charts[i];
            if (chart is null)
            {
                continue;
            }
            if (!string.IsNullOrEmpty(chart.Designer))
            {
                builder.Append("&des_").Append(i + 1).Append('=').AppendLine(chart.Designer);
            }
            if (!string.IsNullOrEmpty(chart.Level))
            {
                builder.Append("&lv_").Append(i + 1).Append('=').AppendLine(chart.Level);
            }
        }
        foreach (var command in simaiFile.Commands)
        {
            builder.Append('&').Append(command.Prefix).Append('=').AppendLine(command.Value);
        }
        for (var i = 0; i < charts.Length; i++)
        {
            var fumen = charts[i]?.Fumen;
            if (string.IsNullOrEmpty(fumen))
            {
                continue;
            }
            builder.Append("&inote_").Append(i + 1).Append('=').AppendLine(fumen);
        }

        return builder.ToString();
    }
}
