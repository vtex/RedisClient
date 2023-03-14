using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedisClient.Core;

internal class ResponseReader
{
    private const bool CONNECTION_CLOSE = true;
    private const bool CONNECTION_KEEP_ALIVE = false;

    private readonly PipeReader _reader;
    private readonly CancellationToken _ct;

    private Status _status = Status.StartParsingType;
    private SequencePosition _consumed;
    private SequencePosition _examined;
    private RespType _type;
    private byte[]? _content;
    private ArrayBufferWriter<byte>? _bulkStringContent;
    private readonly Stack<RespArrayState> _stack = new();
    private RespArrayState? _array;
    private long _bulkStringRemainingSize;

    public RespValue GetContent() => _array != null ? new RespValue(_array.Values.ToArray()) : new RespValue(_type, _content!);

    public ResponseReader(PipeReader reader, CancellationToken ct)
    {
        _reader = reader;
        _ct = ct;
    }

    public async Task<bool> Read()
    {
        ReadResult result;
        do
        {
            var ct = _ct;
            ct.ThrowIfCancellationRequested();
            result = await _reader.ReadAsync(_ct).ConfigureAwait(false);

        } while (TryParseRequest(result));

        if (_reader.TryRead(out result))
        {
            if (result.IsCanceled || result.IsCompleted || !result.Buffer.IsEmpty)
                return CONNECTION_CLOSE;

            _reader.AdvanceTo(result.Buffer.End);
        }

        return CONNECTION_KEEP_ALIVE;
    }

    private bool TryParseRequest(ReadResult result)
    {
        var done = ParseRequest(result);
        if (done)
            return false;

        if (result.IsCanceled)
            return false;

        if (result.IsCompleted)
            return false;

        return true;
    }

    private bool ParseRequest(ReadResult result)
    {
        if (result.Buffer.IsEmpty) Throw("Buffer was empty.");

        ParseRequest(result.Buffer);

        _reader.AdvanceTo(_consumed, _examined);

        return _status == Status.Done;
    }

    private void ParseRequest(ReadOnlySequence<byte> buffer)
    {
        _consumed = buffer.Start;
        _examined = buffer.End;
        switch (_status)
        {
            case Status.StartParsingType:
                _type = GetType(buffer.First.Span[0]);
                buffer = buffer.Slice(1, buffer.End);
                _consumed = buffer.Start;

                if (_type is RespType.SimpleString or RespType.Error or RespType.Integer)
                {
                    _status = Status.ParsingSimpleResponse;
                    goto case Status.ParsingSimpleResponse;
                }
                else if (_type == RespType.BulkString)
                {
                    _status = Status.ParsingBulkStringSize;
                    goto case Status.ParsingBulkStringSize;
                }
                else if (_type == RespType.Array)
                {
                    if (_array != null)
                        _stack.Push(_array);
                    _array = new RespArrayState();
                    _status = Status.ParsingArraySize;
                    goto case Status.ParsingArraySize;
                }
                else
                {
                    Throw("Undefined Type was returned from RedisServer");
                    return; //unreachable
                }
            case Status.ParsingSimpleResponse:
                if (ParseLine(buffer))
                {
                    buffer = buffer.Slice(_consumed, buffer.End);
                    _status = Status.DoneParsingCurrentType;
                    goto case Status.DoneParsingCurrentType;
                }
                else
                {
                    break;
                }
            case Status.ParsingBulkStringSize:
                if (ParseBulkStringSize(buffer))
                {
                    buffer = buffer.Slice(_consumed, buffer.End);
                    _status = Status.ParsingBulkStringContent;
                    goto case Status.ParsingBulkStringContent;
                }
                else
                {
                    break;
                }
            case Status.ParsingBulkStringContent:
                if (_bulkStringRemainingSize == -1)
                {
                    _status = Status.DoneParsingCurrentType;
                    goto case Status.DoneParsingCurrentType;
                }
                if (ParseBulkStringContent(buffer))
                {
                    buffer = buffer.Slice(_consumed, buffer.End);
                    _status = Status.ParsingBulkStringTerminator;
                    goto case Status.ParsingBulkStringTerminator;
                }
                else
                {
                    break;
                }
            case Status.ParsingBulkStringTerminator:
                if (ParseBulkStringTerminator(buffer))
                {
                    buffer = buffer.Slice(_consumed, buffer.End);
                    _status = Status.DoneParsingCurrentType;
                    goto case Status.DoneParsingCurrentType;
                }
                else
                {
                    break;
                }
            case Status.ParsingArraySize:
                if (ParseArraySize(buffer))
                {
                    buffer = buffer.Slice(_consumed, buffer.End);
                    _status = Status.StartParsingType;
                    goto case Status.StartParsingType;
                }
                else
                {
                    break;
                }
            case Status.DoneParsingCurrentType:
                if (_array != null)
                {
                    _array.Remaining--;
                    _array.Values.Add(new RespValue(_type, _content!));

                    if (_array.Remaining > 0)
                    {
                        _status = Status.StartParsingType;
                        goto case Status.StartParsingType;
                    }
                    else
                    {
                        if (_stack.TryPop(out var state))
                        {
                            var current = _array;
                            _array = state;
                            _array.Remaining--;
                            _array.Values.Add(new RespValue(current.Values.ToArray()));
                            if (_array.Remaining > 0)
                            {
                                _status = Status.StartParsingType;
                                goto case Status.StartParsingType;
                            }
                        }
                    }
                }
                _status = Status.Done;
                goto case Status.Done;
            case Status.Done:
                break;
        }
    }

    private bool ParseArraySize(ReadOnlySequence<byte> buffer)
    {
        var done = ParseLine(buffer);
        if (done)
        {
            var success = Utf8Parser.TryParse(_content, out long remainingSize, out _);
            if (!success)
                throw new Exception($"Failed to parse Array size: {_content?.AsEscapedString()} is not valid Int64");
            _array!.Remaining = remainingSize;
        }
        return done;
    }

    private bool ParseBulkStringTerminator(in ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.IsNext(CRLF, true))
        {
            _consumed = reader.Position;
            MemoryMarshal.TryGetArray(_bulkStringContent!.WrittenMemory, out var segment);
            _content = segment.Array;
            _bulkStringContent = null;
            return true;
        }
        return false;
    }

    private bool ParseBulkStringContent(in ReadOnlySequence<byte> buffer)
    {
        _bulkStringContent ??= new ArrayBufferWriter<byte>((int)_bulkStringRemainingSize);

        var actual = (int)Math.Min(buffer.Length, _bulkStringRemainingSize);
        _bulkStringRemainingSize -= actual;

        _consumed = buffer.GetPosition(actual);
        _examined = _consumed;

        var content = buffer.Slice(0, actual);

        var span = _bulkStringContent.GetSpan((int)content.Length);

        content.CopyTo(span);

        _bulkStringContent.Advance((int)content.Length);

        return _bulkStringRemainingSize == 0;
    }

    private bool ParseBulkStringSize(ReadOnlySequence<byte> buffer)
    {
        var done = ParseLine(buffer);
        if (done)
        {
            var success = Utf8Parser.TryParse(_content, out _bulkStringRemainingSize, out _);
            _content = null;
            if (!success)
                Throw($"Failed to parse BulkString size: {_content?.AsEscapedString()} is not valid Int64");
        }
        return done;
    }

    private static ReadOnlySpan<byte> CRLF => new[] { (byte)'\r', (byte)'\n' };

    private bool ParseLine(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadToAny(out ReadOnlySequence<byte> line, CRLF, false) && reader.IsNext(CRLF, true))
        {
            _content = line.ToArray();
            _consumed = reader.Position;
            return true;
        }

        return false;
    }

    private static RespType GetType(byte firstByte) =>
        (char)firstByte switch
        {
            '*' => RespType.Array,
            '$' => RespType.BulkString,
            '-' => RespType.Error,
            ':' => RespType.Integer,
            '+' => RespType.SimpleString,
            _ => RespType.Undefined
        };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw(string message) => throw new Exception(message);

    private enum Status
    {
        StartParsingType,
        ParsingSimpleResponse,
        ParsingBulkStringSize,
        ParsingBulkStringContent,
        ParsingBulkStringTerminator,
        ParsingArraySize,
        DoneParsingCurrentType,
        Done
    }
}