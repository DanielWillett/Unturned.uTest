using System.Diagnostics;
using System.Globalization;

namespace uTest.Runner.Util;

/// <summary>
/// A comparable data structure storing information about a UnityEngine version number.
/// </summary>
[DebuggerDisplay("{Major,nq}.{Minor,nq}.{Build,nq}{Status,nq}{Revision,nq}")]
public readonly struct UnityEngineVersion : IEquatable<UnityEngineVersion>, IComparable, IComparable<UnityEngineVersion>, IFormattable
{
    /// <summary>
    /// 1-5, 2017-2023, or a 6000+ number.
    /// </summary>
    public int Major { get; }
    public int Minor { get; }
    public int Build { get; }

    /// <summary>
    /// Type of release this version is: <c>a</c> (alpha), <c>b</c> (beta), <c>rc</c> (release candidate), <c>f</c> (final), or <c>p</c> (patch)
    /// </summary>
    public string Status { get; }
    public int Revision { get; }

    public UnityEngineVersion(int major, int minor, int build, string status, int revision)
    {
        if (!IsValidMajorVersion(major))
        {
            throw new ArgumentOutOfRangeException(nameof(major));
        }

        Major = major;
        Minor = minor;
        Build = build;
        Status = status;
        Revision = revision;
    }

    public static bool IsValidMajorVersion(int major)
    {
        return major is > 0 and <= 5 or >= 2017 and <= 2023 or >= 6000;
    }

    public int GetStatusLevel()
    {
        if (string.IsNullOrEmpty(Status))
            return 0;

        return Status[0] switch
        {
            'f' => 4,
            'p' => 5,
            'r' => 3,
            'b' => 2,
            'a' => 1,
            _ => 0
        };
    }

    /// <inheritdoc />
    public bool Equals(UnityEngineVersion other) => Major == other.Major
                                                    && Minor == other.Minor
                                                    && Build == other.Build
                                                    && Revision == other.Revision
                                                    && string.Equals(Status, other.Status, StringComparison.Ordinal);

    /// <inheritdoc />
    public int CompareTo(UnityEngineVersion other)
    {
        if (Major > other.Major)
            return 1;
        if (Major < other.Major)
            return -1;
        if (Minor > other.Minor)
            return 1;
        if (Minor < other.Minor)
            return -1;
        if (Build > other.Build)
            return 1;
        if (Build < other.Build)
            return -1;

        int statusLevel = GetStatusLevel();
        int otherStatusLevel = other.GetStatusLevel();

        if (statusLevel > otherStatusLevel)
            return 1;
        if (statusLevel < otherStatusLevel)
            return -1;

        if (Revision > other.Revision)
            return 1;
        if (Revision < other.Revision)
            return -1;

        return statusLevel > 0 ? 0 : string.Compare(Status, other.Status, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}.{Build.ToString(CultureInfo.InvariantCulture)}{Status}{Revision.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is UnityEngineVersion v && Equals(v);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Major;
            hashCode = (hashCode * 397) ^ Minor;
            hashCode = (hashCode * 397) ^ Build;
            if (!string.IsNullOrEmpty(Status))
                hashCode = (hashCode * 397) ^ Status.GetHashCode();
            hashCode = (hashCode * 397) ^ Revision;
            return hashCode;
        }
    }

    /// <inheritdoc />
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj is UnityEngineVersion v ? CompareTo(v) : 1;

    public static UnityEngineVersion Parse(string str)
    {
        if (!TryParse(str ?? throw new ArgumentNullException(nameof(str)), out UnityEngineVersion version))
            throw new FormatException("Inproper Unity Version format.");

        return version;
    }

    public static bool TryParse(string str, out UnityEngineVersion version)
    {
        if (str == null)
        {
            version = default;
            return false;
        }

        int s0 = str.IndexOf('.');
        if (s0 == -1 || s0 == str.Length - 1)
        {
            version = default;
            return false;
        }

        int s1 = str.IndexOf('.', s0 + 1);
        if (s1 == -1 || s1 == str.Length - 1 || s1 == s0 + 1)
        {
            version = default;
            return false;
        }

        int letterIndex = s1 + 1;
        while (char.IsDigit(str[letterIndex]))
        {
            ++letterIndex;
            if (letterIndex < str.Length)
                continue;

            version = default;
            return false;
        }

        if (letterIndex >= str.Length - 1 || letterIndex == s1 + 1)
        {
            version = default;
            return false;
        }

        int revIndex = letterIndex + 1;
        while (!char.IsDigit(str[revIndex]))
        {
            char c = str[revIndex];
            if (!char.IsLetter(c) || char.IsUpper(c))
            {
                version = default;
                return false;
            }

            ++revIndex;
            if (revIndex < str.Length)
            {
                continue;
            }

            version = default;
            return false;
        }

        string v0 = str.Substring(0, s0);
        if (!int.TryParse(v0, NumberStyles.Number, CultureInfo.InvariantCulture, out int major) || !IsValidMajorVersion(major))
        {
            version = default;
            return false;
        }

        string v1 = str.Substring(s0 + 1, s1 - s0 - 1);
        if (!int.TryParse(v1, NumberStyles.Number, CultureInfo.InvariantCulture, out int minor) || minor < 0)
        {
            version = default;
            return false;
        }

        string v2 = str.Substring(s1 + 1, letterIndex - s1 - 1);
        if (!int.TryParse(v2, NumberStyles.Number, CultureInfo.InvariantCulture, out int build) || build < 0)
        {
            version = default;
            return false;
        }

        string rv = str.Substring(revIndex);
        if (!int.TryParse(rv, NumberStyles.Number, CultureInfo.InvariantCulture, out int revision) || revision < 0)
        {
            version = default;
            return false;
        }

        version = new UnityEngineVersion(major, minor, build, str.Substring(letterIndex, revIndex - letterIndex), revision);
        return true;
    }

    public static bool operator ==(UnityEngineVersion left, UnityEngineVersion right) => left.Equals(right);
    public static bool operator !=(UnityEngineVersion left, UnityEngineVersion right) => !left.Equals(right);
    public static bool operator <(UnityEngineVersion left, UnityEngineVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(UnityEngineVersion left, UnityEngineVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(UnityEngineVersion left, UnityEngineVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(UnityEngineVersion left, UnityEngineVersion right) => left.CompareTo(right) >= 0;
}