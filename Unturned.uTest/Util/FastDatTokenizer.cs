using System;

namespace uTest;

// taken from https://github.com/DanielWillett/unturned-asset-file-vscode/blob/master/UnturnedAssetSpec/Files/DatTokenizer.cs

internal ref struct FastDatTokenizer
{
    private readonly ReadOnlySpan<char> _file;

    private char _currentChar;
    private bool _hasChar;
    private int _position;
    private int _lineNumber;
    private int _columnNumber;

    /// <summary>
    /// The current token.
    /// </summary>
    public DatToken Token;

    public FastDatTokenizer(ReadOnlySpan<char> data)
    {
        _file = data;
        _position = -1;
        Token.StartLine = 1;
        Token.StartColumn = 1;
        _lineNumber = 1;
        _columnNumber = 0;
    }

    public void Reset()
    {
        _position = -1;
        _currentChar = default;
        _hasChar = false;
        Token = default;
        Token.StartLine = 1;
        Token.StartColumn = 1;
        _lineNumber = 1;
        _columnNumber = 0;
    }

    public bool MoveNext()
    {
        ReadOnlySpan<char> key;

        if (_position == -1)
        {
            ReadChar();
            SkipUtf8Bom();
            SkipWhitespaceAndComments();
            while (_hasChar)
            {
                if (_currentChar == '/')
                {
                    SkipWhitespaceAndComments();
                }
                else
                {
                    Token.Quoted = _currentChar == '"';
                    key = ReadDictionaryKey(out Token.StartIndex, out Token.Length, out Token.StartColumn, out Token.StartLine);
                    SkipSpacesAndTabs();
                    Token.Type = DatTokenType.Key;
                    Token.Content = key;
                    return true;
                }
            }

            Token.Type = DatTokenType.None;
            return false;
        }

        while (_hasChar)
        {
            Token.Quoted = false;
            ReadOnlySpan<char> value;
            switch (Token.Type)
            {
                case DatTokenType.Key:
                    bool quoted = _currentChar == '"';
                    int colNum = _columnNumber, lineNum = _lineNumber;
                    value = ReadString(out Token.StartIndex, out Token.Length, out Token.StartColumn, out Token.StartLine);
                    SkipWhitespaceAndComments();
                    if (_currentChar is '{' or '[' || !quoted && value.Length == 0)
                    {
                        //if (_diagnostics != null && (quoted || !value.IsWhiteSpace()))
                        //{
                        //
                        //}
                        goto case DatTokenType.Value;
                    }
                    Token.Type = DatTokenType.Value;
                    Token.Content = value;
                    Token.Quoted = quoted;
                    return true;

                case DatTokenType.DictionaryStart:
                    while (_hasChar)
                    {
                        if (_currentChar == '/')
                        {
                            SkipWhitespaceAndComments();
                            continue;
                        }

                        if (_currentChar == '}')
                        {
                            Token.Type = DatTokenType.DictionaryEnd;
                            Token.Content = _file.Slice(_position, 1);
                            Token.StartColumn = _columnNumber;
                            Token.StartLine = _lineNumber;
                            Token.StartIndex = _position;
                            Token.Length = 1;
                            SkipWhitespaceAndComments();
                            return true;
                        }

                        Token.Quoted = _currentChar == '"';
                        key = ReadDictionaryKey(out Token.StartIndex, out Token.Length, out Token.StartColumn, out Token.StartLine);
                        SkipSpacesAndTabs();
                        Token.Type = DatTokenType.Key;
                        Token.Content = key;
                        return true;
                    }

                    SkipWhitespaceAndComments();
                    Token.Type = DatTokenType.None;
                    return false;

                case DatTokenType.ListStart:
                case DatTokenType.ListValue:
                    while (_hasChar)
                    {
                        if (_currentChar == '/')
                        {
                            SkipWhitespaceAndComments();
                            continue;
                        }

                        if (_currentChar == ']')
                        {
                            Token.Type = DatTokenType.ListEnd;
                            Token.Content = _file.Slice(_position, 1);
                            Token.StartIndex = _position;
                            Token.StartColumn = _columnNumber;
                            Token.StartLine = _lineNumber;
                            Token.Length = 1;
                            ReadChar();
                            SkipWhitespaceAndComments();
                            return true;
                        }

                        if (_currentChar == '{')
                        {
                            Token.Type = DatTokenType.DictionaryStart;
                            Token.Content = _file.Slice(_position, 1);
                            Token.StartIndex = _position;
                            Token.StartColumn = _columnNumber;
                            Token.StartLine = _lineNumber;
                            Token.Length = 1;
                            ReadChar();
                            SkipWhitespaceAndComments();
                            return true;
                        }

                        if (_currentChar == '[')
                        {
                            Token.Type = DatTokenType.ListStart;
                            Token.Content = _file.Slice(_position, 1);
                            Token.StartIndex = _position;
                            Token.StartColumn = _columnNumber;
                            Token.StartLine = _lineNumber;
                            Token.Length = 1;
                            ReadChar();
                            SkipWhitespaceAndComments();
                            return true;
                        }

                        Token.Quoted = _currentChar == '"';
                        value = ReadString(out Token.StartIndex, out Token.Length, out Token.StartColumn, out Token.StartLine);
                        SkipWhitespaceAndComments();
                        Token.Type = DatTokenType.ListValue;
                        Token.Content = value;
                        return true;
                    }

                    SkipWhitespaceAndComments();
                    Token.Type = DatTokenType.None;
                    return false;

                case DatTokenType.DictionaryEnd:
                case DatTokenType.ListEnd:
                case DatTokenType.Value:
                    if (!_hasChar)
                    {
                        Token.Type = DatTokenType.None;
                        return false;
                    }

                    if (_currentChar == '{')
                    {
                        Token.Type = DatTokenType.DictionaryStart;
                        Token.Content = _file.Slice(_position, 1);
                        Token.StartIndex = _position;
                        Token.StartColumn = _columnNumber;
                        Token.StartLine = _lineNumber;
                        Token.Length = 1;
                        ReadChar();
                        SkipWhitespaceAndComments();
                        return true;
                    }

                    if (_currentChar == ']')
                    {
                        Token.Type = DatTokenType.ListEnd;
                        Token.Content = _file.Slice(_position, 1);
                        Token.StartIndex = _position;
                        Token.StartColumn = _columnNumber;
                        Token.StartLine = _lineNumber;
                        Token.Length = 1;
                        ReadChar();
                        SkipWhitespaceAndComments();
                        return true;
                    }

                    if (_currentChar == '}')
                    {
                        Token.Type = DatTokenType.DictionaryEnd;
                        Token.Content = _file.Slice(_position, 1);
                        Token.StartIndex = _position;
                        Token.StartColumn = _columnNumber;
                        Token.StartLine = _lineNumber;
                        Token.Length = 1;
                        ReadChar();
                        SkipWhitespaceAndComments();
                        return true;
                    }

                    if (_currentChar == '[')
                    {
                        Token.Type = DatTokenType.ListStart;
                        Token.Content = _file.Slice(_position, 1);
                        Token.StartIndex = _position;
                        Token.StartColumn = _columnNumber;
                        Token.StartLine = _lineNumber;
                        Token.Length = 1;
                        ReadChar();
                        SkipWhitespaceAndComments();
                        return true;
                    }

                    Token.Quoted = _currentChar == '"';
                    key = ReadDictionaryKey(out Token.StartIndex, out Token.Length, out Token.StartColumn, out Token.StartLine);
                    SkipSpacesAndTabs();
                    Token.Type = DatTokenType.Key;
                    Token.Content = key;
                    return true;

            }
        }

        Token.Type = DatTokenType.None;
        return false;
    }

    private void SkipSpacesAndTabs()
    {
        while (_hasChar && _currentChar is ' ' or '\t')
            ReadChar();
    }

    private void ReadChar()
    {
        if (_position + 1 >= _file.Length)
        {
            _position = _file.Length;
            _hasChar = false;
            _currentChar = default;
            return;
        }

        ++_position;
        _hasChar = true;
        _currentChar = _file[_position];

        if (_currentChar == '\n')
        {
            _columnNumber = 0;
            ++_lineNumber;
        }
        else
        {
            ++_columnNumber;
        }
    }

    private void SkipUtf8Bom()
    {
        if (!_hasChar || _currentChar != 'ï')
            return;
        ReadChar();
        if (!_hasChar || _currentChar != '»')
            return;
        ReadChar();
        if (!_hasChar || _currentChar != '¿')
            return;
        ReadChar();
    }

    private void SkipWhitespaceAndComments()
    {
    restart:
        while (_hasChar)
        {
            if (_currentChar == '/')
            {
                ReadChar();
                while (true)
                {
                    if (_hasChar && _currentChar != '\n' && _currentChar != '\r')
                        ReadChar();
                    else
                        goto restart;
                }
            }

            if (!char.IsWhiteSpace(_currentChar) && _currentChar != ',')
                break;

            ReadChar();
        }
    }

    private ReadOnlySpan<char> ReadDictionaryKey(out int startIndex, out int length, out int startColumn, out int startLine)
    {
        if (_currentChar == '"')
            return ReadQuotedString(out startIndex, out length, out startColumn, out startLine);

        int oldPos = _position;
        startColumn = _columnNumber;
        startLine = _lineNumber;
        while (_hasChar && !char.IsWhiteSpace(_currentChar))
        {
            ReadChar();
        }

        startIndex = oldPos;
        length = _position - oldPos;
        return _file.Slice(oldPos, length);
    }

    private ReadOnlySpan<char> ReadQuotedString(out int startIndex, out int length, out int startColumn, out int startLine)
    {
        ReadChar();
        bool nextIsEscapeSequence = false;
        bool foundEndQuote = false;

        int oldPos = _position;
        startColumn = _columnNumber;
        startLine = _lineNumber;
        while (_hasChar)
        {
            if (nextIsEscapeSequence)
            {
                if (_currentChar == 'n')
                    _currentChar = '\n';
                else if (_currentChar == 't')
                    _currentChar = '\t';
                //else if (_currentChar != '\\' && _currentChar != '"' && _diagnostics != null)
                //{
                //
                //}
            }
            else
            {
                if (_currentChar == '"')
                {
                    ReadChar();
                    foundEndQuote = true;
                    break;
                }
                if (_currentChar == '\\')
                {
                    nextIsEscapeSequence = true;
                    ReadChar();
                    continue;
                }
            }
            nextIsEscapeSequence = false;

            ReadChar();
        }

        startIndex = oldPos;
        length = _position - oldPos + (foundEndQuote ? -1 : 0);
        ReadOnlySpan<char> value = _file.Slice(oldPos, length);

        //if (!foundEndQuote && _diagnostics != null)
        //{
        //
        //}

        return value;
    }

    private ReadOnlySpan<char> ReadString(out int startIndex, out int length, out int startColumn, out int startLine)
    {
        if (_currentChar == '"')
            return ReadQuotedString(out startIndex, out length, out startColumn, out startLine);

        bool nextIsEscapeSequence = false;

        int oldPos = _position;
        startColumn = _columnNumber;
        startLine = _lineNumber;

        while (_hasChar)
        {
            if (nextIsEscapeSequence)
            {
                if (_currentChar == 'n')
                    _currentChar = '\n';
                else if (_currentChar == 't')
                    _currentChar = '\t';
                //else if (_currentChar != '\\' && _diagnostics != null)
                //{
                //
                //}
            }
            else if (_currentChar != '\r' && _currentChar != '\n')
            {
                if (_currentChar == '\\')
                {
                    nextIsEscapeSequence = true;
                    ReadChar();
                    continue;
                }
            }
            else
                break;

            nextIsEscapeSequence = false;
            ReadChar();
        }

        startIndex = oldPos;
        length = _position - oldPos;
        return _file.Slice(oldPos, length);
    }
}

public ref struct DatToken
{
    public DatTokenType Type;
    public ReadOnlySpan<char> Content;
    public bool Quoted;
    public int StartIndex;
    public int StartLine;
    public int StartColumn;
    public int Length;
}

public enum DatTokenType
{
    None,
    Key,
    Value,
    ListValue,
    DictionaryStart,
    DictionaryEnd,
    ListStart,
    ListEnd
}