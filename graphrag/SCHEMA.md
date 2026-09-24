# Schema đồ thị tri thức — vụ án Chris–Danie

Đồ thị này biểu diễn **tri thức của từng NPC**, không phải một bộ suy luận thay người chơi.
Cạnh mang tính lập luận được phép tồn tại, nhưng chỉ trong khuôn khổ tài liệu mà NPC đó được đọc.

Tri thức lấy từ [`corpus/`](../corpus/) (11 file, 45 dòng). Ai được đọc file nào lấy từ bảng
"Nhân vật dùng file nào" trong [`HUONG_DAN.md`](../HUONG_DAN.md), và được suy ra tự động từ
front matter `nguon` + `loai` — không gõ tay ở đâu cả.

## File

| File | Vai trò |
|---|---|
| `ontology.json` | **Nguồn sự thật duy nhất** của tên nhãn, tên loại quan hệ và giá trị `trang_thai`. Code C# không chứa một chuỗi nào trong số đó — chúng đi vào Cypher dưới dạng tham số. |
| `entities.json` | 118 thực thể |
| `relationships.json` | 179 cạnh; loại đối xứng khai **một chiều**, bộ nạp phát chiều còn lại → 185 cạnh trong Neo4j |
| `neo4j-init.cypher` | 1 constraint + 3 index. Chạy **một lần**, trước lần nạp đầu |
| `neo4j-load.cypher` | Dữ liệu dạng Cypher, sinh ra từ ba file trên. Không sửa tay |
| `backup/` | Bản chụp đồ thị trước mỗi lần thay thế |

## Nhãn

`ThucThe` là nhãn nền trên **mọi** node, nên truy vấn mở rộng không phải sửa khi thêm loại thực thể.
`NhanVat` là nhãn **thứ hai** đặt lên chính node `:Nguoi` tương ứng, không phải node riêng — tách ra
thì họ tên phải khớp tuyệt đối ở hai nơi, sai một dấu là NPC mất sạch tri thức mà Cypher không một
cảnh báo.

| Nhãn | Số | Ghi chú |
|---|---|---|
| `NhanDinh` | 25 | Một **phát biểu** của ai đó, không phải sự thật. Mang `nguoi_khai`, `do_tin_cay` |
| `VatChung` | 20 | `trong_so_tay` cho biết vật đó có vào sổ tay người chơi không |
| `Nguoi` | 15 | 13 trong số này mang thêm nhãn `NhanVat` |
| `SuKien` | 14 | |
| `DiaDiem` | 12 | |
| `DacDiem` | 11 | |
| `ThoiGian` | 9 | `thu_tu` để dựng dòng thời gian |
| `GiaThuyet` | 7 | Điểm quy tụ của `UNG_HO` / `MAU_THUAN` |
| `Nhom` | 5 | Cặp đôi, tổ cảnh sát, người dân khu phố, gia đình Johny, ba mẹ Johny |

## Nhân vật

Corpus ghi **vai trò**, còn mọi nơi khác — payload `npcNames` của Qdrant, `duoc_biet` trong đồ thị,
field `npcName` của endpoint — dùng **tên riêng**. Lúc nạp corpus, `EntityAliasNpcNameResolver` đổi
vai trò sang tên riêng qua `bi_danh`. Đổi tên một NPC chỉ phải sửa một dòng trong `entities.json`, rồi
nạp lại corpus.

| Vai trò trong corpus | Tên trong đồ thị | Đọc được |
|---|---|---|
| Cảnh sát 1 | **James** | `00`, `10` |
| Cảnh sát 2 | **Michael** | `00`, `10` |
| Wills | **Wills** | `00`, `11`, `20` |
| Người đi đường 1 | **Daniel** | `00`, `11` |
| Người đi đường 2 | **Thomas** | `00`, `11` |
| Người dân ngồi bên đường 1 | **Arthur** | `00`, `12` |
| Người dân ngồi bên đường 2 | **Clara** | `00`, `12` |
| Brad | **Brad** | `00`, `21` |
| Ông lão tiệm bánh mì | **Bread** | `00`, `22` |
| Johny | **Johny** | `00`, `23` |
| Bà Johny | **Margaret** | `00`, `24` |
| Pháp y | **Edward** | `00`, `25` |
| Jack | **Jack** | `00`, `26` |

> Đồ thị **không biết ai là thủ phạm**. Johny mang `vai_tro: ["nhan_chung"]`. Mắt xích trận bóng
> (`23_johny#L2`) chỉ tồn tại dưới dạng đường đi — tấm ảnh ngoại phạm ghi lại một trận bóng, và
> một chiếc đồng hồ xác định thời điểm của chính tấm ảnh đó. Không cạnh nào kết luận hộ.

## Thuộc tính hệ thống

| Thuộc tính | Trên | Ý nghĩa |
|---|---|---|
| `ten` | node | Khóa định danh, có constraint UNIQUE |
| `nguon_chunk` | node + cạnh | Mã chunk `<doc_id>#L<n>`. **Khóa ghép** với kho vector |
| `bi_danh` | node | Tên gọi khác: hiện trong danh mục cho LLM trích thực thể, đối chiếu tên LLM trả về, và ánh xạ vai trò → tên riêng |
| `duoc_biet` | node `:NhanVat` | Danh sách **mã tài liệu** NPC được đọc. Bộ nạp ghi, không gõ tay |
| `trang_thai` | cạnh | `xac_nhan` → `loi_khai` → `tin_don` → `suy_doan` |
| `phu_dinh` | cạnh | Cạnh này **phủ định** quan hệ của nó |

### `nguon_chunk` của một node là **mọi** dòng nhắc tới nó

Không phải riêng dòng giới thiệu nó. Suy ra từ định nghĩa: nếu một cạnh rút từ dòng X và một đầu mút
của nó là E, thì E có mặt trong dòng X.

Bỏ qua luật này là một lớp lỗi im lặng. Ví dụ thật: node `Johny` khai ở `23_johny#L1`, còn Margaret
chỉ đọc `00` và `24` — tầng lọc quyền trên node lập tức chặn Margaret khỏi **chính cháu mình**, và
bốn cạnh trong lời khai của bà biến mất khỏi đồ thị mà không một cảnh báo nào.

### `phu_dinh` là thuộc tính, không phải loại quan hệ

Corpus có **7 chỗ phủ định**, trải khắp lời khai, khám nghiệm và ngoại phạm. Đặt tên
`KHONG_CO_DAU_VET_CAM` cho từng cái sẽ nhân đôi bảng từ vựng, và mọi truy vấn *"ai có dấu vết cầm
súng"* phải nhớ liệt kê cả biến thể phủ định.

Thiếu nó thì `25_phap-y#L3` — *"trên tay Danie **không** có dấu hiệu cầm súng"* — vào đồ thị thành
lời khẳng định ngược hẳn, mang `trang_thai: xac_nhan`, và NPC pháp y sẽ nói với người chơi đúng điều
trái với kết quả khám nghiệm của chính mình.

Ontology cho mỗi loại quan hệ một `nhan_hien_thi` và, nơi nào phủ định có nghĩa, một
`nhan_hien_thi_phu_dinh`.

## Bốn bất biến của mọi truy vấn phục vụ người chơi

Bỏ sót cái nào cũng **không gây lỗi** — chỉ cho kết quả sai trong im lặng. Vì vậy cả bốn nằm trong
**một** hằng Cypher duy nhất, `Class/Constants/GraphCypher.cs`.

**1. Lọc quyền ba lần: node hạt giống, cạnh, và node lân cận.** Node lân cận mới là thứ đi vào
prompt; nếu nó xuất thân từ tài liệu NPC không đọc được thì chính sự xuất hiện của nó đã là rò tri
thức. Hạt giống vẫn phải lọc dù LLM chỉ được chọn trong danh mục đã lọc quyền: tên đi qua LLM, và
không gì trên đường đó được tin mà không lọc lại. Danh mục (`KnownEntitiesTemplate`) chịu đúng bộ lọc
này — tên một thực thể NPC không được biết cũng đã là tri thức.

Với quan hệ **mang lập luận** thì phải là `all`, không phải `any`:

```cypher
AND (CASE WHEN type(r) IN $loaiLapLuan
          THEN all(m IN r.nguon_chunk WHERE split(m,'#')[0] IN npc.duoc_biet)
          ELSE any(m IN r.nguon_chunk WHERE split(m,'#')[0] IN npc.duoc_biet) END)
```

Đây là dạng cơ học của luật *"lập luận được, nhưng trong khuôn khổ được biết"*. Một cạnh bắc cầu
giữa hai hồ sơ chỉ có nghĩa với NPC đọc được **cả hai** đầu. Dùng `any`, Edward sẽ "biết" cảnh sát
kết luận gì dù chưa từng đọc hồ sơ cảnh sát.

**2. Khử trùng cạnh đối xứng — so theo chiều LƯU TRỮ.**

```cypher
AND NOT (type(r) IN $loaiDoiXung AND startNode(r).ten > endNode(r).ten)
```

Không phải `e.ten > lc.ten` của chiều duyệt: hai bản gương nối cùng một cặp node, nên khi duyệt từ
hạt giống thì cả hai cho cùng một `(e, lc)` và cùng qua — mệnh đề chỉ dịch chỗ trùng lặp chứ không
bỏ được nó, và *"Chris quen biết Jack"* vào prompt hai lần.

**3. Lọc `trang_thai`.** Cạnh từ tin đồn hàng xóm phải phân biệt được với cạnh từ khám nghiệm. Không
lọc, LLM trả lời tin đồn như thể là kết luận — trong game trinh thám đó là bịa chứng cứ.

**4. Cắt trước khi nhồi prompt**, **chia lượt theo hạt giống** và với khóa sắp **đủ để tất định**.

Hạt giống là **tên thực thể** do LLM chọn trong danh mục NPC được biết, theo thứ tự trọng tâm mà LLM
trả về (`$thucThe`), kèm các loại quan hệ câu hỏi muốn biết (`$loaiQuanTam`). Hạt giống không lấy từ
nhánh vector: hai nhánh chạy song song, nên vector trượt dòng thì đồ thị không trượt theo.

Mỗi cạnh thuộc nhóm của thực thể hạt giống tốt nhất mà nó mọc ra (`hang`). Trong nhóm sắp

```
(noi_hai, khop_y_dinh, ưu tiên trạng thái, lập luận trước, startNode.ten, type(r), endNode.ten)
```

rồi lấy **xen kẽ**: cạnh số 1 của mọi nhóm, cạnh số 2 của mọi nhóm, ...

- `noi_hai = 0` khi cạnh nối **hai** thực thể cùng được hỏi. *"Tay Chris có dấu vết gì"* hỏi về
  Chris và Khẩu súng thì cạnh Chris–Khẩu súng là câu trả lời, không phải một cạnh bất kỳ quanh Chris.
- `khop_y_dinh = 0` khi loại cạnh nằm trong `$loaiQuanTam`. Đây là **ưu tiên**, không phải lọc: câu
  hỏi về dấu vết vẫn có thể cần biết ai sở hữu khẩu súng.

Không chia lượt thì một thực thể nhiều cạnh (một địa điểm, một nhân vật trung tâm) chiếm hết `LIMIT`
của các thực thể còn lại. Thiếu một vế của khóa sắp là cùng câu hỏi cho hai prompt khác nhau, rồi
cache đóng băng một biến thể ngẫu nhiên và bài đo mất tính lặp lại.

**Node của chính NPC được làm hạt giống** (`e = npc`) khi LLM chọn nó — tức khi câu hỏi nói về chính
NPC (*"trong phòng cậu có gì"*). Danh mục luôn chứa chính NPC, kể cả khi node NPC chỉ xuất hiện trong
tài liệu mà NPC không đọc; nếu không, câu hỏi xưng "anh/cậu" sẽ mất hạt giống.

Lưu ý đo lường: LLM trích thực thể chạy với temperature khác 0 và không có cache kết quả trích, nên
endpoint debug (đi vòng qua cache câu trả lời) có thể chọn hạt giống khác nhau giữa hai lần gọi. Trên
đường `/ask`, câu hỏi lặp lại trúng cache câu trả lời trước khi tới bước trích.

## Nạp

**Qua app** (khuyên dùng — đây là đường duy nhất ghi `duoc_biet`):

```
POST /api/graph/indexes     # một lần
POST /api/graph/load
GET  /api/graph/verify      # phải ra matches: true
```

Cần `Graph:Enabled = true` và `Graph:Loader:Enabled = true`, ở môi trường Development.

**Qua cypher-shell** (khi không chạy app; **không** ghi phân quyền):

```
cypher-shell -a <uri> -u <user> -p <pass> -f neo4j-init.cypher
cypher-shell -a <uri> -u <user> -p <pass> -f neo4j-load.cypher
```

Bộ nạp **MERGE-only**, chạy lại bao nhiêu lần cũng một kết quả. Đổi lại, gỡ một cạnh khỏi file nguồn
thì phải xóa tay; `verify` báo lệch. Thay cả bản thì xóa dữ liệu trước — schema không bị ảnh hưởng:

```cypher
MATCH (n) DETACH DELETE n
```

## Số liệu chốt

| Phép đo | Giá trị |
|---|---|
| Tài liệu / chunk / NPC | 11 / 45 / 13 |
| Thực thể | 118 |
| Cạnh gold → trong Neo4j | 179 → 185 (6 đối xứng nhân đôi) |
| Loại quan hệ | 34 (3 đối xứng, 10 mang lập luận, 9 có dạng phủ định) |
| Dòng corpus được ít nhất một cạnh dùng | **45 / 45** |
| Cạnh có ít nhất một NPC thấy được | **185 / 185** |
| Cạnh NPC thấy nhiều nhất → ít nhất | Edward 51 → James/Michael 26 |
