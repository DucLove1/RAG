namespace RAG.Interface
{
    /// <summary>
    /// Cắt một văn bản dài thành các đoạn để nhúng.
    /// <para>
    /// Là interface chứ không phải lớp static như bản trước: cách cắt đoạn ảnh hưởng trực tiếp tới
    /// chất lượng truy hồi, nên đây đúng là thứ sẽ được thay đi thử lại nhiều lần. Static thì không
    /// thay được cài đặt mà không sửa nơi gọi.
    /// </para>
    /// </summary>
    public interface IChunkingStrategy
    {
        IEnumerable<string> Chunk(string text);
    }
}
