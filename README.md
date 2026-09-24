# Bộ tri thức vụ án Chris & Danie

Kho này phục vụ hai pipeline chạy song song để so sánh trong KLTN: **baseline RAG** và
**GraphRAG**.

```
RAG_KB/
├── corpus/         ← BẢN DOC. Văn thuần, một dòng = một chunk.
│   └── 11 file .md tri thức nhân vật
├── graphrag/       ← ĐỒ THỊ TRI THỨC. Không chứa văn bản, không chứa vector.
│   ├── SCHEMA.md            thiết kế đồ thị + truy vấn mẫu
│   ├── graph-schema.json    nguồn sự thật cho pipeline
│   ├── ONTOLOGY.md          quy ước viết tài liệu, loại thực thể/quan hệ, tên chuẩn hóa
│   ├── PROPERTIES.md        bảng tra đầy đủ mọi thuộc tính (sinh tự động)
│   ├── entities.json        111 thực thể đã extract
│   ├── relationships.json   183 quan hệ đã extract, kèm xuất xứ từng cạnh
│   ├── entities-by-doc.json thực thể theo từng tài liệu, gợi ý cho prompt extract
│   ├── build_graph.py       kiểm tra dữ liệu + sinh graph-data.cypher
│   ├── graph-data.cypher    sinh tự động, nạp thẳng được vào Neo4j
│   └── neo4j-init.cypher    constraint, fulltext index, phân quyền NPC
├── HUONG_DAN.md    ← Thiết kế game. KHÔNG nạp vào RAG.
└── README.md
```

---

## Nhánh 1 — Baseline RAG

Đọc `corpus/`, không cần gì trong `graphrag/`.

```python
import glob, yaml, pathlib

chunks = []
for path in sorted(glob.glob("corpus/*.md")):
    _, fm, than = pathlib.Path(path).read_text(encoding="utf-8").split("---", 2)
    meta = yaml.safe_load(fm)
    for i, dong in enumerate([d.strip() for d in than.split("\n") if d.strip()], start=1):
        chunks.append({
            "id": f'{meta["doc_id"]}#L{i}',
            "text": dong,
            "metadata": {k: meta[k] for k in
                         ("doc_id", "tieu_de", "nguon", "loai", "scene", "do_tin_cay")},
        })
# 45 chunk. Embed truong "text", loc bang metadata["nguon"] khi mot NPC tra loi.
```

Không cần splitter theo token, theo câu hay theo heading — tài liệu đã được viết sẵn sao cho
**một dòng là một đơn vị ngữ nghĩa trọn vẹn**. Những câu chỉ có nghĩa khi đi cùng nhau đã
được đặt trên cùng một dòng.

Quy ước viết dòng: xem [graphrag/ONTOLOGY.md §2](graphrag/ONTOLOGY.md).

## Nhánh 2 — GraphRAG

Đọc **cùng `corpus/` đó**, cộng thêm schema và bộ extract trong `graphrag/`.

```bash
python graphrag/build_graph.py     # kiểm tra theo schema + sinh graphrag/graph-data.cypher
```

1. Chạy [graphrag/neo4j-init.cypher](graphrag/neo4j-init.cypher) khối 1–3 — constraint, index
   tra cứu, fulltext index.
2. Chạy `graphrag/graph-data.cypher` — 111 thực thể và 183 quan hệ.
3. Chạy khối 4 của `neo4j-init.cypher` — tầng access, giới hạn tài liệu mỗi NPC đọc được.

**Đồ thị chỉ chứa tri thức.** Không có node văn bản, không có embedding, không có vector
index — cắt dòng và truy xuất ngữ nghĩa đều là việc của nhánh 1. Mọi node và cạnh mang
`nguon_chunk` = danh sách mã dòng, đó là đường truy ngược về `corpus/`.

Toàn bộ bước 1–3 tất định, không gọi LLM: kết quả extract đã có sẵn trong
[entities.json](graphrag/entities.json) và [relationships.json](graphrag/relationships.json).
Muốn *đo* chất lượng LLM extract thì chạy extract từ corpus theo
[graph-schema.json](graphrag/graph-schema.json) rồi đối chiếu với hai file đó.

Chi tiết và code mẫu: [graphrag/SCHEMA.md §4](graphrag/SCHEMA.md).

---

## Vì sao hai nhánh dùng chung một corpus

Đây là điểm sẽ bị hỏi khi bảo vệ. **Baseline RAG và GraphRAG phải đọc đúng cùng một văn
bản**, nếu không thì chênh lệch kết quả có thể đến từ việc corpus khác nhau chứ không phải
từ kiến trúc truy xuất — và toàn bộ so sánh mất giá trị.

Vì vậy `corpus/` không chứa bất kỳ dấu vết nào của đồ thị: không triple, không mã chunk,
không danh sách thực thể. Mọi thứ phục vụ riêng GraphRAG đều nằm trong `graphrag/`. Hai
nhánh khác nhau **chỉ ở tầng xử lý**, không khác ở đầu vào.

Biến duy nhất còn lại là thứ bạn muốn đo: baseline chỉ có vector search trên 45 dòng rời rạc,
GraphRAG lấy đúng kết quả đó rồi mở rộng bằng tri thức quanh các thực thể trong dòng. Dòng ở
đây rất ngắn (1–3 câu), nên chênh lệch lộ rõ nhất ở những câu hỏi cần ghép thông tin từ nhiều
lời khai — ví dụ *"có gì mâu thuẫn với kết luận tự sát của cảnh sát không"*.

## Ghép hai nhánh lại

Hai nhánh dùng **chung một mã dòng** `<doc_id>#L<n>`: nhánh 1 lấy nó làm `id` trong vector
store, nhánh 2 lấy nó làm `nguon_chunk` trên node và trên cạnh. Bước ghép chỉ là truyền một
danh sách chuỗi — vector store trả về mã dòng kèm văn bản, đồ thị nhận mã đó và trả về tri
thức quanh nó (thực thể, quan hệ, rồi các cạnh lập luận `UNG_HO` / `MAU_THUAN`).

Phân công rõ ràng, không chồng lấn:

| Việc | Nhánh nào |
|---|---|
| Lưu văn bản, embedding, vector search | RAG |
| Dòng liền kề (`#L3`, `#L5`) — tính bằng số học từ mã | RAG |
| Thực thể, quan hệ, giả thuyết, mâu thuẫn | GraphRAG |
| Phân quyền NPC | Cả hai đều phải tôn trọng |

Không cần đồng bộ gì thêm, chừng nào cả hai còn đọc chung `corpus/` và sinh mã bằng cùng một
công thức. Chi tiết và Cypher mẫu: [graphrag/SCHEMA.md §5.0](graphrag/SCHEMA.md).

## Thống kê corpus

| File | Nguồn | Loại | Số chunk |
|---|---|---|---|
| `00_boi-canh-chung.md` | Bối cảnh chung | `boi_canh` | 4 |
| `10_chung_canh-sat.md` | Cảnh sát 1, Cảnh sát 2 | `ket_luan_chuyen_mon` | 2 |
| `11_chung_tin-don-tieng-la-het.md` | Wills, Người đi đường | `tin_don` | 2 |
| `12_chung_tin-don-khoa-minh-trong-nha.md` | Người dân ngồi bên đường | `tin_don` | 2 |
| `20_wills.md` | Wills | `loi_khai_npc` | 2 |
| `21_brad.md` | Brad | `loi_khai_npc` | 4 |
| `22_ong-lao-tiem-banh-mi.md` | Ông lão tiệm bánh mì | `loi_khai_npc` | 5 |
| `23_johny.md` | Johny | `loi_khai_npc` | 6 |
| `24_ba-johny.md` | Bà Johny | `loi_khai_npc` | 3 |
| `25_phap-y.md` | Pháp y | `ket_luan_chuyen_mon` | 10 |
| `26_jack.md` | Jack | `loi_khai_npc` | 5 |
| | | **Tổng** | **45** |

Nhân vật nào được đọc file nào: xem bảng trong [HUONG_DAN.md](HUONG_DAN.md). Cả hai nhánh
đều phải tôn trọng bảng này, nếu không NPC sẽ trả lời tri thức mà NPC đó không được biết.
