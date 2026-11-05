using System;
using System.Collections.Generic;

namespace MajSimai;

/// <summary>
/// A field of a simai file that MajSimai does not read itself, such as <c>&amp;wholebpm=180</c>.
/// </summary>
public readonly struct SimaiCommand : IEquatable<SimaiCommand>
{
    /// <summary>
    /// Creates a command.
    /// </summary>
    /// <param name="prefix">The field name, without <c>&amp;</c>.</param>
    /// <param name="value">The field value; <see langword="null"/> becomes an empty string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is empty.</exception>
    public SimaiCommand(string prefix, string? value)
    {
        if (prefix is null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }
        if (prefix.Length == 0)
        {
            throw new ArgumentException("\"prefix\" cannot be empty", nameof(prefix));
        }
        Prefix = prefix;
        Value = value ?? string.Empty;
    }

    /// <summary>
    /// Gets the field name, without <c>&amp;</c>.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the field value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Reads a command from a line such as <c>&amp;wholebpm=180</c>. The name and the value are trimmed.
    /// </summary>
    /// <param name="content">The line, starting with <c>&amp;</c>.</param>
    /// <param name="cmd">Receives the command when the line could be read.</param>
    /// <returns><see langword="true"/> when the line has a <c>=</c> and a name.</returns>
    public static bool TryParse(ReadOnlySpan<char> content, out SimaiCommand cmd)
    {
        cmd = default;
        var index = content.IndexOf('=');
        if (index < 1)
        {
            return false;
        }
        var prefix = content.Slice(1, index - 1).Trim();
        if (prefix.IsEmpty)
        {
            return false;
        }
        cmd = new SimaiCommand(new string(prefix), new string(content.Slice(index + 1).Trim()));

        return true;
    }

    /// <summary>
    /// Compares two commands by name and value.
    /// </summary>
    /// <param name="other">The other command.</param>
    /// <returns><see langword="true"/> when both name and value are equal.</returns>
    public bool Equals(SimaiCommand other)
    {
        return string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
               string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compares this command with an object.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="SimaiCommand"/>.</returns>
    public override bool Equals(object? obj)
    {
        return obj is SimaiCommand other && Equals(other);
    }

    /// <summary>
    /// Gets a hash code from the name and the value.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Prefix, Value);
    }

    /// <summary>
    /// Compares two commands by name and value.
    /// </summary>
    /// <param name="left">The first command.</param>
    /// <param name="right">The second command.</param>
    /// <returns><see langword="true"/> when both are equal.</returns>
    public static bool operator ==(SimaiCommand left, SimaiCommand right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two commands by name and value.
    /// </summary>
    /// <param name="left">The first command.</param>
    /// <param name="right">The second command.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(SimaiCommand left, SimaiCommand right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Converts a command to a name and value pair.
    /// </summary>
    /// <param name="cmd">The command.</param>
    /// <returns>The pair of <see cref="Prefix"/> and <see cref="Value"/>.</returns>
    public static implicit operator KeyValuePair<string, string>(SimaiCommand cmd)
    {
        return new KeyValuePair<string, string>(cmd.Prefix, cmd.Value);
    }
}
