namespace NetMph;

public interface IKeySource
{
    uint KeyCount { get; }
    Span<byte> Read();
    void Rewind();
}