using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Phần cấu hình chỉ có nghĩa với provider <c>Faiss</c> của tầng cache ngữ nghĩa.
    /// <para>
    /// Toàn bộ các núm ở đây tồn tại vì FAISS chạy TRONG tiến trình: nó không có TTL, không có
    /// chính sách đuổi, và không có đĩa. Redis cho không cả ba thứ đó (hạn dùng của key,
    /// <c>maxmemory</c> + <c>volatile-lru</c> trong docker-compose.yml, và RDB); ở đây ứng dụng
    /// phải tự làm, nên mỗi mục dưới đây là bản thay thế của một thứ Redis vốn lo hộ.
    /// </para>
    /// <para>
    /// Lý do tách thành class riêng thay vì lồng vào class cha: xem
    /// <see cref="SemanticAnswerCacheRedisConfig"/>.
    /// </para>
    /// </summary>
    public class SemanticAnswerCacheFaissConfig
    {
        /// <summary>Dẫn xuất từ section cha, cùng lý do với <see cref="SemanticAnswerCacheRedisConfig.SectionName"/>.</summary>
        public const string SectionName = SemanticAnswerCacheConfig.SectionName + ":Faiss";

        /// <summary>
        /// Nơi ghi cache xuống đĩa. ĐỂ RỖNG = cache chỉ sống trong RAM (cùng quy ước với
        /// <see cref="QueryCacheConfig.PersistPath"/>); khi đó service ghi đĩa không được đăng ký.
        /// <para>
        /// Trong container phải trỏ vào Disk gắn ngoài (Dockerfile đặt sẵn
        /// <c>/var/data/answer-cache.bin</c>), không thì mỗi lần deploy là cache rỗng lại từ đầu.
        /// Khác với provider Redis: ở đó cache nằm ngoài tiến trình nên không liên quan gì tới
        /// Disk — đây đúng là chỗ hai provider đòi hỏi hạ tầng khác nhau.
        /// </para>
        /// </summary>
        public string PersistPath { get; set; } = "App_Data/answer-cache.bin";

        /// <summary>
        /// Nhịp ghi đĩa, ĐỒNG THỜI là nhịp quét entry hết hạn. Gộp làm một là có chủ ý: một timer,
        /// một mục cấu hình, và nhờ quét chạy ngay trước khi ghi nên entry đã chết không bao giờ
        /// bị ghi xuống đĩa.
        /// <para>
        /// Đây cũng là cửa sổ rủi ro của lệnh xoá cache: ghi theo kiểu write-behind nên purge xong
        /// mà container bị SIGKILL trước nhịp flush kế tiếp thì câu trả lời vừa xoá sẽ SỐNG LẠI từ
        /// file ở lần khởi động sau. Tắt êm (docker stop) thì có flush lúc StopAsync nên không sao.
        /// Muốn cửa sổ hẹp hơn thì hạ con số này.
        /// </para>
        /// </summary>
        [Range(5, 3600)]
        public int FlushIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// Trần số entry của toàn bộ cache. Vượt trần thì nhịp quét loại bớt entry lâu không dùng
        /// nhất.
        /// <para>
        /// BẮT BUỘC phải có: đây là bản thay thế cho <c>maxmemory</c> + <c>volatile-lru</c> mà
        /// docker-compose.yml đặt cho Redis. Trong tiến trình .NET không có ai chặn trần hộ, và
        /// khác <c>MemoryQueryCache</c> thì <c>MemoryCache</c> cũng không đứng ra cứu vì FAISS giữ
        /// vector trong bộ nhớ native, ngoài tầm với của nó.
        /// </para>
        /// <para>
        /// Ước lượng: mỗi entry tốn khoảng 7 KB ở 768 chiều (3 KB vector trong FAISS + 3 KB bản
        /// sao dùng để dựng lại index và chấm điểm + phần chữ). 5000 entry ≈ 35 MB.
        /// </para>
        /// </summary>
        [Range(100, 1_000_000)]
        public int MaxEntries { get; set; } = 5000;

        /// <summary>
        /// Trần số phân vùng. Mỗi cặp (tên NPC, mô tả tính cách) là một phân vùng — tức một index
        /// FAISS riêng.
        /// <para>
        /// Tồn tại vì mô tả tính cách do CLIENT gửi lên theo TỪNG request. Một client lỗi (hoặc cố
        /// ý) đổi persona mỗi lần sẽ đẻ phân vùng không giới hạn cho tới khi hết RAM. Chạm trần
        /// thì đường GHI từ chối tạo phân vùng mới và đếm vào <c>errors</c>; đường ĐỌC không bao
        /// giờ tạo phân vùng nên không cần chặn.
        /// </para>
        /// </summary>
        [Range(1, 10_000)]
        public int MaxPartitions { get; set; } = 128;

        /// <summary>
        /// Số láng giềng lấy về mỗi lượt tra.
        /// <para>
        /// LỚN HƠN 1, khác bản Redis (<c>AnswerCacheFields.NeighborCount = 1</c>), và đây là khác
        /// biệt bắt buộc chứ không phải tuỳ chọn: Redis xoá entry hết hạn ngay ở phía server nên
        /// láng giềng gần nhất luôn là một entry còn sống. Ở FAISS entry hết hạn vẫn nằm trong
        /// index cho tới nhịp quét kế tiếp, nên lấy đúng 1 nghĩa là một entry đã chết CHẶN ĐƯỜNG
        /// một entry còn sống ngay phía sau nó — trượt oan kéo dài tới trọn một
        /// <see cref="FlushIntervalSeconds"/>.
        /// </para>
        /// <para>
        /// Đường đọc vẫn DỪNG ở ứng viên còn sống ĐẦU TIÊN. Nếu nó dưới ngưỡng thì đó là trượt,
        /// KHÔNG đi tiếp xuống ứng viên thứ hai — đi tiếp là âm thầm nới lỏng recall so với Redis.
        /// </para>
        /// </summary>
        [Range(1, 32)]
        public int NeighborCount { get; set; } = 3;
    }
}
