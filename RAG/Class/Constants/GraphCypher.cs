namespace RAG.Class.Constants
{
    /// <summary>
    /// Mọi câu Cypher phục vụ người chơi.
    /// <para>
    /// Gom về MỘT chỗ vì bốn mệnh đề bắt buộc bên dưới có chung một tính chất: bỏ sót cái nào cũng
    /// KHÔNG gây lỗi, chỉ cho kết quả sai trong im lặng. Viết tay từng câu ở từng nơi gọi nghĩa là
    /// sớm muộn sẽ có một câu thiếu một mệnh đề, và không gì báo.
    /// </para>
    /// <para>
    /// Chỉ TÊN NHÃN được <c>string.Format</c> vào (Cypher không nhận nhãn làm tham số), và chúng
    /// lấy từ ontology đã kiểm chứ không bao giờ từ dữ liệu. Mọi thứ khác — danh sách trạng thái,
    /// danh sách loại đối xứng, danh sách loại mang lập luận, bảng ưu tiên — đều là THAM SỐ.
    /// </para>
    /// </summary>
    public static class GraphCypher
    {
        public static class Parameters
        {
            public const string Npc = "npc";
            public const string Entities = "thucThe";
            public const string IntentTypes = "loaiQuanTam";
            public const string AllowedStatuses = "trangThaiChoPhep";
            public const string SymmetricTypes = "loaiDoiXung";
            public const string ReasoningTypes = "loaiLapLuan";
            public const string StatusPriority = "uuTienTrangThai";
            public const string ReasoningOnly = "chiLapLuan";
            public const string Limit = "gioiHan";
            public const string Rows = "rows";
        }

        public static class Columns
        {
            public const string Id = "id";
            public const string Source = "nguon";
            public const string Relation = "quan_he";
            public const string Target = "dich";
            public const string Status = "trang_thai";
            public const string ChunkCodes = "ma_chunk";
            public const string Priority = "uu_tien";
            public const string Reasoning = "lap_luan";
            public const string Negated = "phu_dinh";
            public const string Name = "ten";
            public const string Count = "so_luong";
            public const string Aliases = "bi_danh";
            public const string Labels = "nhan";
            public const string IsSelf = "ban_than";
        }

        /// <summary>
        /// Danh mục thực thể mà một NPC được biết. <c>{0}</c> = nhãn NPC, <c>{1}</c> = nhãn nền.
        /// <para>
        /// Đây là "thực đơn" đưa cho LLM trích thực thể: nó chỉ được chọn trong danh sách này. Vì vậy
        /// danh mục PHẢI chịu đúng bộ lọc quyền của truy vấn mở rộng — tên của một thực thể mà NPC
        /// không được biết cũng đã là tri thức, và để nó lọt vào prompt trích là rò qua cửa sau.
        /// </para>
        /// <para>
        /// <c>e = npc</c> đưa chính NPC vào danh mục của mình bất kể <c>nguon_chunk</c> của node đó.
        /// Câu hỏi xưng "anh/cậu" nói về chính NPC, và nếu node NPC chỉ xuất hiện trong tài liệu mà
        /// NPC không đọc thì thiếu vế này là mất hạt giống. <c>ban_than</c> đánh dấu dòng đó để lấy
        /// TÊN CHUẨN điền vào prompt, kể cả khi endpoint được gọi bằng bí danh.
        /// </para>
        /// </summary>
        public const string KnownEntitiesTemplate = """
            MATCH (npc:{0}) WHERE npc.ten = $npc OR $npc IN coalesce(npc.bi_danh, [])
            MATCH (e:{1})
            WHERE e = npc
               OR any(m IN e.nguon_chunk WHERE split(m, '#')[0] IN npc.duoc_biet)
            RETURN DISTINCT e.ten                   AS ten,
                            coalesce(e.bi_danh, []) AS bi_danh,
                            labels(e)               AS nhan,
                            e = npc                 AS ban_than
            ORDER BY ten
            """;

        /// <summary>
        /// Mở rộng một bậc quanh các thực thể hạt giống. <c>{0}</c> = nhãn NPC, <c>{1}</c> = nhãn nền.
        /// <para>
        /// Hạt giống là TÊN thực thể do LLM chọn trong danh mục NPC được biết
        /// (<see cref="KnownEntitiesTemplate"/>), theo thứ tự trọng tâm mà LLM trả về
        /// (<c>$thucThe</c>). <c>$loaiQuanTam</c> là các loại quan hệ câu hỏi muốn biết — dùng để
        /// ƯU TIÊN cạnh, không để lọc: một câu hỏi về dấu vết vẫn có thể cần biết ai sở hữu khẩu súng.
        /// </para>
        /// <para>
        /// BỐN MỆNH ĐỀ BẮT BUỘC, đừng gỡ cái nào:
        /// </para>
        /// <para>
        /// 1. LỌC QUYỀN BA LẦN — node hạt giống, node lân cận, và cạnh. Lọc hai lần là chưa đủ: node
        /// lân cận mới là thứ đi vào prompt, và nếu nó xuất thân từ tài liệu NPC không đọc được thì
        /// chính sự xuất hiện của nó đã là rò tri thức. Với cạnh MANG LẬP LUẬN thì phải là
        /// <c>all</c> chứ không <c>any</c>: một cạnh bắc cầu giữa hai tài liệu chỉ có nghĩa với NPC
        /// đọc được CẢ HAI đầu. Dùng <c>any</c>, pháp y sẽ "biết" cảnh sát kết luận gì dù chưa từng
        /// đọc hồ sơ cảnh sát — một suy luận đúng về logic nhưng NPC không có quyền thực hiện. Lọc
        /// hạt giống vẫn cần dù danh mục đã lọc: tên đi qua LLM, và không gì ở đây được tin mà không
        /// lọc lại.
        /// </para>
        /// <para>
        /// 2. KHỬ TRÙNG CẠNH ĐỐI XỨNG — loại đối xứng nằm cả hai chiều trong DB để truy vấn đi từ
        /// phía nào cũng thấy; truy vấn vô hướng khớp cả hai, tức cùng một sự thật vào prompt hai
        /// lần. Chỉ áp cho loại đối xứng: với loại bất đối xứng thì chiều CHÍNH LÀ nghĩa.
        /// </para>
        /// <para>
        /// 3. LỌC TRẠNG THÁI — danh sách cho phép đến từ ontology. Mặc định gồm cả tin đồn, vì tin
        /// đồn là toàn bộ tri thức của mấy NPC hàng xóm; thứ ngăn mô hình trình bày tin đồn như kết
        /// luận là nhãn hiển thị đi kèm từng dòng trong prompt.
        /// </para>
        /// <para>
        /// 4. SẮP TOÀN PHẦN RỒI MỚI CẮT — <c>ORDER BY</c> chỉ theo trạng thái là KHÔNG TẤT ĐỊNH:
        /// hàng chục cạnh cùng trạng thái, Neo4j trả tập nào cũng đúng. Cùng câu hỏi ra prompt khác
        /// nhau, rồi cache câu trả lời đóng băng một biến thể ngẫu nhiên và mọi phép đo mất tính
        /// lặp lại. Khóa sắp phải xuống tới tên hai đầu mới đủ.
        /// </para>
        /// <para>
        /// CHIA LƯỢT THEO HẠT GIỐNG. Mỗi cạnh được xếp vào nhóm của thực thể hạt giống TỐT NHẤT mà nó
        /// mọc ra từ đó (<c>hang</c> = vị trí của thực thể trong <c>$thucThe</c>). Trong nhóm, thứ
        /// tự là:
        /// <c>noi_hai</c> — cạnh nối HAI thực thể cùng được hỏi đứng trước ("tay Chris có dính súng
        /// không" thì cạnh Chris–Khẩu súng là câu trả lời, không phải một cạnh bất kỳ quanh Chris);
        /// rồi <c>khop_y_dinh</c> — cạnh đúng loại quan hệ câu hỏi muốn biết; rồi mới tới trạng thái
        /// và lập luận. Cuối cùng lấy XEN KẼ: cạnh số 1 của mọi nhóm, cạnh số 2 của mọi nhóm, ...
        /// (<c>vong</c>), để một thực thể nhiều cạnh không chiếm hết <c>LIMIT</c> của thực thể kia.
        /// Mỗi cạnh luôn có sẵn một đầu là hạt giống, nên <c>noi_hai</c> chỉ cần hỏi cả hai đầu có
        /// nằm trong <c>$thucThe</c> không — không cần gom nhóm.
        /// </para>
        /// <para>
        /// Trả <c>startNode(r)</c> / <c>endNode(r)</c> chứ KHÔNG phải <c>e</c>: mẫu <c>(e)-[r]-(lc)</c>
        /// là vô hướng, nên khi hạt giống là ĐÍCH của cạnh, trả <c>e</c> làm nguồn sẽ render ngược
        /// nghĩa ("Khẩu súng có dấu vết cầm súng trên Chris"). <c>WITH r, min(hang)</c> gộp một
        /// cạnh mọc ra từ hai hạt giống về MỘT dòng, giữ nhóm của hạt giống tốt hơn.
        /// </para>
        /// <para>
        /// Mệnh đề khử trùng đối xứng so <c>startNode(r).ten</c> với <c>endNode(r).ten</c>, tức là
        /// CHIỀU LƯU TRỮ, chứ không phải <c>e.ten &gt; lc.ten</c> của chiều duyệt. Khác biệt này từng
        /// là một lỗi thật: hai bản gương nối cùng một cặp node, nên khi duyệt từ hạt giống thì cả
        /// hai đều cho cùng một <c>(e, lc)</c> và cùng qua hoặc cùng trượt — mệnh đề chỉ dịch chỗ
        /// trùng lặp chứ không bỏ được nó, và "Chris quen biết Jack" vào prompt hai lần. So theo
        /// chiều lưu trữ thì đúng MỘT trong hai bản gương sống sót, bất kể hạt giống là đầu nào.
        /// Điều đó dựa vào việc bộ nạp LUÔN phát đủ hai chiều cho loại đối xứng: khai tay một chiều
        /// với <c>tu &gt; den</c> thì cạnh đó biến mất hẳn — và đó chính là lý do validator cấm khai
        /// hai chiều còn bộ nạp giữ độc quyền việc nhân bản.
        /// </para>
        /// <para>
        /// Node của CHÍNH NPC ĐƯỢC làm hạt giống (<c>e = npc</c>), khác bản gieo hạt theo mã chunk
        /// trước đây. Hồi đó NPC lọt vào qua MỌI dòng lời khai của mình nên phải chặn; giờ nó chỉ là
        /// hạt giống khi LLM chọn nó — tức khi câu hỏi nói về chính NPC ("trong phòng cậu có gì") — và
        /// <c>khop_y_dinh</c> giữ các cạnh liên quan ở trên đầu nhóm của nó.
        /// </para>
        /// <para>
        /// NPC khớp theo <c>ten</c> HOẶC <c>bi_danh</c>. Endpoint gọi NPC bằng TÊN RIÊNG ("Edward"),
        /// nhưng khớp thêm bí danh để một client còn gửi VAI TRÒ trong corpus ("Pháp y") không lặng lẽ
        /// ra 0 cạnh. Ngoặc nhọn của map thuộc tính viết <c>{{ }}</c> vì chuỗi này đi qua
        /// <c>string.Format</c>: một ngoặc đơn bị đọc thành placeholder và app không khởi động được.
        /// </para>
        /// </summary>
        public const string ExpandTemplate = """
            MATCH (npc:{0}) WHERE npc.ten = $npc OR $npc IN coalesce(npc.bi_danh, [])
            UNWIND range(0, size($thucThe) - 1) AS hang
            MATCH (e:{1} {{ ten: $thucThe[hang] }})
            WHERE e = npc
               OR any(m IN e.nguon_chunk WHERE split(m, '#')[0] IN npc.duoc_biet)
            MATCH (e)-[r]-(lc:{1})
            WHERE r.trang_thai IN $trangThaiChoPhep
              AND any(m IN lc.nguon_chunk WHERE split(m, '#')[0] IN npc.duoc_biet)
              AND (CASE WHEN type(r) IN $loaiLapLuan
                        THEN all(m IN r.nguon_chunk WHERE split(m, '#')[0] IN npc.duoc_biet)
                        ELSE any(m IN r.nguon_chunk WHERE split(m, '#')[0] IN npc.duoc_biet) END)
              AND NOT (type(r) IN $loaiDoiXung AND startNode(r).ten > endNode(r).ten)
              AND (NOT $chiLapLuan OR type(r) IN $loaiLapLuan)
            WITH r, min(hang) AS hang
            WITH r, hang,
                 startNode(r).ten AS nguon,
                 type(r)          AS quan_he,
                 endNode(r).ten   AS dich,
                 coalesce($uuTienTrangThai[r.trang_thai], 99) AS uu_tien,
                 CASE WHEN type(r) IN $loaiLapLuan THEN 0 ELSE 1 END AS lap_luan,
                 CASE WHEN startNode(r).ten IN $thucThe AND endNode(r).ten IN $thucThe THEN 0 ELSE 1 END AS noi_hai,
                 CASE WHEN type(r) IN $loaiQuanTam THEN 0 ELSE 1 END AS khop_y_dinh
            ORDER BY hang, noi_hai, khop_y_dinh, uu_tien, lap_luan, nguon, quan_he, dich
            WITH hang, collect({{ r: r, nguon: nguon, quan_he: quan_he, dich: dich,
                                  uu_tien: uu_tien, lap_luan: lap_luan }}) AS nhom
            UNWIND range(0, size(nhom) - 1) AS vong
            WITH hang, vong, nhom[vong] AS x
            RETURN elementId(x.r)                AS id,
                   x.nguon                       AS nguon,
                   x.quan_he                     AS quan_he,
                   x.dich                        AS dich,
                   x.r.trang_thai                AS trang_thai,
                   coalesce(x.r.phu_dinh, false) AS phu_dinh,
                   x.r.nguon_chunk               AS ma_chunk,
                   x.uu_tien                     AS uu_tien,
                   x.lap_luan                    AS lap_luan
            ORDER BY vong, hang
            LIMIT $gioiHan
            """;

        /// <summary>
        /// Ghi node theo lô. <c>{0}</c> = nhãn nền, <c>{1}</c> = chuỗi nhãn phụ dạng <c>:A:B</c>.
        /// <para>
        /// Nhãn phải <c>string.Format</c> vào vì Cypher không nhận nhãn làm tham số. Chuỗi nhãn LUÔN
        /// dựng từ ontology đã kiểm, KHÔNG BAO GIỜ từ dòng dữ liệu — nếu không, một dòng JSON có thể
        /// chèn Cypher tùy ý. Giá trị thì ngược lại, luôn là tham số.
        /// </para>
        /// </summary>
        public const string MergeEntitiesTemplate = """
            UNWIND $rows AS row
            MERGE (e:{0} {{ ten: row.ten }})
            SET e{1}
            SET e += row.props
            """;

        /// <summary>Ghi cạnh theo lô. <c>{0}</c> = nhãn nền, <c>{1}</c> = loại quan hệ.</summary>
        public const string MergeRelationshipsTemplate = """
            UNWIND $rows AS row
            MATCH (a:{0} {{ ten: row.tu }})
            MATCH (b:{0} {{ ten: row.den }})
            MERGE (a)-[r:{1}]->(b)
            SET r += row.props
            """;

        /// <summary>
        /// Ghi phân quyền. <c>{0}</c> = nhãn nền, <c>{1}</c> = nhãn NPC.
        /// <para>
        /// Dùng <c>SET n.duoc_biet =</c> chứ TUYỆT ĐỐI không <c>+=</c>. Cộng dồn vào một thuộc tính
        /// dạng danh sách tạo ra một ACL chỉ-lớn-dần: gỡ một tài liệu khỏi quyền của NPC sẽ không có
        /// tác dụng gì, và đó là một lỗ bảo mật mà không test nào bắt được vì mọi thứ vẫn chạy đúng.
        /// </para>
        /// <para>
        /// <c>MATCH</c> chứ không <c>MERGE</c> node: NPC phải đã tồn tại như một thực thể trong
        /// <c>entities.json</c>. <c>MERGE</c> ở đây sẽ âm thầm tạo node thứ hai khi tên lệch một dấu
        /// tiếng Việt, và NPC đó mất sạch tri thức domain trong khi Cypher không một lời cảnh báo.
        /// </para>
        /// </summary>
        public const string MergeAccessTemplate = """
            UNWIND $rows AS row
            MATCH (n:{0} {{ ten: row.ten }})
            SET n:{1}
            SET n.duoc_biet = row.duoc_biet
            """;

        public const string CountEntitiesTemplate = "MATCH (e:{0}) RETURN count(e) AS so_luong";

        public const string CountNpcsTemplate = "MATCH (n:{0}) RETURN count(n) AS so_luong";

        public const string CountRelationships = "MATCH ()-[r]->() RETURN count(r) AS so_luong";

        public const string CountByRelationType =
            "MATCH ()-[r]->() RETURN type(r) AS quan_he, count(*) AS so_luong ORDER BY quan_he";

        /// <summary>
        /// Constraint UNIQUE trên tên thực thể. <c>{0}</c> = tên constraint, <c>{1}</c> = nhãn nền.
        /// <para>
        /// PHẢI tồn tại TRƯỚC lần nạp đầu tiên. <c>MERGE</c> không có constraint vẫn có thể đẻ node
        /// trùng dưới đồng thời, và Neo4j từ chối thêm constraint sau khi đã có bản trùng — lúc đó
        /// phải dọn tay.
        /// </para>
        /// </summary>
        public const string CreateNameConstraintTemplate =
            "CREATE CONSTRAINT {0} IF NOT EXISTS FOR (e:{1}) REQUIRE e.ten IS UNIQUE";

        public const string CreateNpcIndexTemplate =
            "CREATE INDEX {0} IF NOT EXISTS FOR (n:{1}) ON (n.ten)";

        public const string CreateFulltextNameTemplate =
            "CREATE FULLTEXT INDEX {0} IF NOT EXISTS FOR (e:{1}) ON EACH [e.ten]";

        public const string CreateFulltextAliasTemplate =
            "CREATE FULLTEXT INDEX {0} IF NOT EXISTS FOR (e:{1}) ON EACH [e.bi_danh]";
    }
}
