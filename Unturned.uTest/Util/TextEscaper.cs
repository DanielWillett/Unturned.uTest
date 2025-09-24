using System;

namespace uTest;

internal class TextEscaper
{
    private readonly char[] _escapables;
    private readonly CharMap[]? _customMappings;

    internal char[] Escapables => _escapables;

    public bool IsEscapeSequenceChar(char c)
    {
        if (_customMappings != null)
        {
            for (int i = 0; i < _customMappings.Length; ++i)
            {
                ref CharMap map = ref _customMappings[i];
                if (map.Result == c)
                    return true;
            }
        }

        return Array.IndexOf(Escapables, c) >= 0 && c is not ('\0' or '\t' or '\n' or '\v' or '\r');
    }

    public TextEscaper(char[] escapables, CharMap[] customMappings)
    {
        Array.Sort(escapables, (a, b) => a - b);
        Array.Sort(customMappings, (a, b) => a.Key.CompareTo(b.Key));
        _escapables = escapables;
        _customMappings = customMappings;
    }

    public TextEscaper(params char[] escapables)
    {
        Array.Sort(escapables, (a, b) => a - b);
        _escapables = escapables;

        int mask = (IsEscapable('\0') ? 1 : 0)
                   | (IsEscapable('\t') ? 2 : 0)
                   | (IsEscapable('\n') ? 4 : 0)
                   | (IsEscapable('\v') ? 8 : 0)
                   | (IsEscapable('\r') ? 16 : 0);

        int size = ((mask & 1) == 1 ? 1 : 0)
                   + ((mask & 2) == 2 ? 1 : 0)
                   + ((mask & 4) == 4 ? 1 : 0)
                   + ((mask & 8) == 8 ? 1 : 0)
                   + ((mask & 16) == 16 ? 1 : 0);

        if (size == 0)
        {
            _customMappings = null;
            return;
        }

        // needs to be in descending order of the key
        _customMappings = new CharMap[size];
        if ((mask & 16) != 0)
            _customMappings[--size] = new CharMap('\r', 'r');
        if ((mask & 8) != 0)
            _customMappings[--size] = new CharMap('\v', 'v');
        if ((mask & 4) != 0)
            _customMappings[--size] = new CharMap('\n', 'n');
        if ((mask & 2) != 0)
            _customMappings[--size] = new CharMap('\t', 't');
        if ((mask & 1) != 0)
            _customMappings[--size] = new CharMap('\0', '0');
    }

    protected char GetMappingFromEscapeChar(char c)
    {
        if (_customMappings == null)
            return c;

        for (int i = 0; i < _customMappings.Length; ++i)
        {
            ref CharMap map = ref _customMappings[i];
            if (map.Result == c)
                return map.Key;
        }

        return c;
    }

    protected char GetMapping(char c)
    {
        if (_customMappings == null)
            return c;

        int lo = 0;
        int hi = _customMappings.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            char escapable = _customMappings[mid].Key;
            if (c == escapable)
                return _customMappings[mid].Result;

            if (escapable < c)
                lo = mid + 1;
            else hi = mid - 1;
        }

        return c;
    }

    protected bool IsEscapable(char c)
    {
        int lo = 0;
        int hi = _escapables.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            char escapable = _escapables[mid];
            if (c == escapable)
                return true;

            if (escapable < c)
                lo = mid + 1;
            else hi = mid - 1;
        }

        return false;
    }

    public string Escape(ReadOnlySpan<char> value)
    {
        return Escape(value, null);
    }

    public string Escape(string value)
    {
        return Escape(value, value);
    }

    protected virtual string Escape(ReadOnlySpan<char> value, string? valueAsString)
    {
        int c = 0;
        ReadOnlySpan<char> s = value;
        for (int i = 0; i < s.Length; ++i)
        {
            if (IsEscapable(s[i]))
                ++c;
        }

        if (c <= 0)
        {
            return valueAsString ?? value.ToString();
        }

        unsafe
        {
            char* newValue = stackalloc char[s.Length + c];

            int prevIndex = -1;
            int writeIndex = 0;
            while (true)
            {
                int startIndex = prevIndex + 1;
                int index = s.Slice(startIndex).IndexOfAny(_escapables);
                if (index == -1)
                {
                    for (int i = prevIndex + 1; i < s.Length; ++i)
                    {
                        newValue[writeIndex] = s[i];
                        ++writeIndex;
                    }
                    break;
                }

                index += startIndex;
                for (int i = prevIndex + 1; i < index; ++i)
                {
                    newValue[writeIndex] = s[i];
                    ++writeIndex;
                }

                newValue[writeIndex] = '\\';
                newValue[writeIndex + 1] = GetMapping(s[index]);

                writeIndex += 2;

                prevIndex = index;
            }

            return new string(newValue, 0, writeIndex);
        }
    }

    public string Unescape(ReadOnlySpan<char> value)
    {
        return Unescape(value, null, 0, value.Length);
    }

    public string Unescape(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        return Unescape(value, value, 0, value.Length);
    }

    public string Unescape(string value, int start, int length)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (start < 0 || start > value.Length)
            throw new ArgumentOutOfRangeException(nameof(start));

        if (length > value.Length - start)
            throw new ArgumentOutOfRangeException(nameof(length));

        return Unescape(value.AsSpan(start, length), value, start, length);
    }

    private static int IndexOf(ReadOnlySpan<char> value, char c, int start)
    {
        int ind = value[start..].IndexOf(c);
        if (ind == -1)
            return -1;
        return ind + start;
    }

    protected virtual unsafe string Unescape(ReadOnlySpan<char> value, string? valueAsString, int start, int length)
    {
        int fullLength = length + start;

        int firstSlash = IndexOf(value, '\\', start);
        if (firstSlash == -1 || firstSlash >= fullLength)
        {
            return valueAsString != null ? valueAsString.Substring(start, length) : value.ToString();
        }

        char* newValue = stackalloc char[length];

        int prevIndex = start - 1;
        int slashCount = 0;
        int writeIndex = 0;
        while (true)
        {
            int nextSlash = prevIndex != fullLength - 1
                ? prevIndex == start - 1
                    ? firstSlash
                    : IndexOf(value, '\\', prevIndex + 1)
                : -1;
            if (nextSlash >= fullLength)
                nextSlash = -1;
            if (nextSlash == prevIndex + 1)
            {
                ++slashCount;
            }
            else if (nextSlash == -1)
            {
                if (prevIndex + 1 >= fullLength || slashCount == 0)
                {
                    for (int i = prevIndex + 1; i < fullLength; ++i)
                    {
                        newValue[writeIndex] = value[i];
                        ++writeIndex;
                    }

                    break;
                }
            }
            else
            {
                slashCount = 1;
            }

            int max = nextSlash - slashCount + 1;
            for (int i = prevIndex + 1; i < max; ++i)
            {
                newValue[writeIndex] = value[i];
                ++writeIndex;
            }

            if (slashCount == 1)
            {
                if (nextSlash < fullLength - 1)
                {
                    char newChar = GetMappingFromEscapeChar(value[nextSlash + 1]);
                    newValue[writeIndex] = newChar;
                    ++writeIndex;
                    ++nextSlash;
                }
                else
                {
                    newValue[writeIndex] = '\\';
                    ++writeIndex;
                    break;
                }

                slashCount = 0;
            }

            if (nextSlash == -1)
                break;

            prevIndex = nextSlash;
        }

        return new string(newValue, 0, writeIndex);
    }

    public struct CharMap
    {
        public char Key;
        public char Result;
        public CharMap(char key, char result)
        {
            Key = key;
            Result = result;
        }
    }
}
