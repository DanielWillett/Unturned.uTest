using System;

namespace uTest.Util;

internal class TextEscaper
{
    private readonly char[] _escapables;
    private readonly CharMap[]? _customMappings;

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

        int mask = (IsEscapable('\t') ? 1 : 0)
                   | (IsEscapable('\n') ? 2 : 0)
                   | (IsEscapable('\v') ? 4 : 0)
                   | (IsEscapable('\r') ? 8 : 0)
                   | (IsEscapable('\0') ? 16 : 0);

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

        _customMappings = new CharMap[size];
        if ((mask & 16) != 0)
            _customMappings[--size] = new CharMap('\0', '0');
        if ((mask & 8) != 0)
            _customMappings[--size] = new CharMap('\r', 'r');
        if ((mask & 4) != 0)
            _customMappings[--size] = new CharMap('\v', 'v');
        if ((mask & 2) != 0)
            _customMappings[--size] = new CharMap('\n', 'n');
        if ((mask & 1) != 0)
            _customMappings[--size] = new CharMap('\t', 't');
    }

    protected char GetMapping(char c)
    {
        if (_customMappings == null)
            return c;

        int lo = 0;
        int hi = _customMappings.Length;
        while (lo < hi)
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
        int hi = _escapables.Length;
        while (lo < hi)
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

    public virtual string Escape(string value)
    {
        int c = 0;
        string s = value;
        for (int i = 0; i < s.Length; ++i)
        {
            if (IsEscapable(s[i]))
                ++c;
        }

        if (c <= 0)
        {
            return s;
        }

        unsafe
        {
            char* newValue = stackalloc char[s.Length + c];

            int prevIndex = -1;
            int writeIndex = 0;
            while (true)
            {
                int index = s.IndexOfAny(_escapables, prevIndex + 1);
                if (index == -1)
                {
                    for (int i = prevIndex + 1; i < s.Length; ++i)
                    {
                        newValue[writeIndex] = s[i];
                        ++writeIndex;
                    }
                    break;
                }

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