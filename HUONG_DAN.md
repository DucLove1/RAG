# Hướng dẫn sử dụng bộ tài liệu

File này **không nạp vào RAG**. Các file còn lại chỉ chứa thông tin mà nhân vật biết.

Quy ước viết file tri thức (frontmatter, chunking, loại thực thể, vị từ quan hệ, tên chuẩn
hóa) nằm ở [graphrag/ONTOLOGY.md](graphrag/ONTOLOGY.md).

Schema đồ thị cho GraphRAG nằm trong thư mục [graphrag/](graphrag/): [SCHEMA.md](graphrag/SCHEMA.md)
giải thích thiết kế và truy vấn mẫu, [graph-schema.json](graphrag/graph-schema.json) là nguồn
sự thật cho pipeline, [neo4j-init.cypher](graphrag/neo4j-init.cypher) tạo constraint và index.

## Nhân vật dùng file nào

| Nhân vật | Scene | File được nạp |
|---|---|---|
| Cảnh sát 1 | 1 | `00`, `10` |
| Cảnh sát 2 | 1 | `00`, `10` |
| Wills | 1 | `00`, `11`, `20` |
| Người đi đường 1 | 2 | `00`, `11` |
| Người đi đường 2 | 2 | `00`, `11` |
| Người dân ngồi bên đường 1 | 2 | `00`, `12` |
| Người dân ngồi bên đường 2 | 2 | `00`, `12` |
| Brad | 2 | `00`, `21` |
| Ông lão chủ tiệm bánh mì | 2 | `00`, `22` |
| Jack (chủ tiệm sửa xe) | 2 | `00`, `26` |
| Johny | 2, 5 | `00`, `23` |
| Bà Johny | 5 | `00`, `24` |
| Pháp y | 4 | `00`, `25` |

## File dùng chung cho nhiều nhân vật

| File | Nhân vật cùng biết |
|---|---|
| `00_boi-canh-chung.md` | Tất cả |
| `10_chung_canh-sat.md` | Cảnh sát 1, Cảnh sát 2 |
| `11_chung_tin-don-tieng-la-het.md` | Wills, Người đi đường 1, Người đi đường 2 |
| `12_chung_tin-don-khoa-minh-trong-nha.md` | Người dân ngồi bên đường 1, Người dân ngồi bên đường 2 |

Thông tin nhiều nhân vật cùng biết chỉ nằm ở một file duy nhất, không lặp lại trong file
riêng của từng nhân vật.

## Danh sách file

| File | Số dòng tri thức | Nội dung |
|---|---|---|
| `00_boi-canh-chung.md` | 4 | Bối cảnh vụ án, ai là nạn nhân, hiện trường |
| `10_chung_canh-sat.md` | 2 | Kết luận giết người rồi tự sát |
| `11_chung_tin-don-tieng-la-het.md` | 2 | Tiếng la hét và đập phá từ nhà cặp đôi |
| `12_chung_tin-don-khoa-minh-trong-nha.md` | 2 | Cặp đôi kỳ dị, khóa mình trong nhà, tin đồn Chris đánh Danie |
| `20_wills.md` | 2 | Hai tiếng súng cách nhau 15 phút, nhận định về Chris |
| `21_brad.md` | 4 | Quá khứ và tính cách Chris, quan hệ với Danie |
| `22_ong-lao-tiem-banh-mi.md` | 5 | Thói quen của Chris, mốc 4–5 tháng, không có chứng cứ ngoại phạm |
| `23_johny.md` | 6 | Bằng chứng ngoại phạm, các tấm ảnh, đôi găng tay |
| `24_ba-johny.md` | 3 | Bà Johny là bà của Johny, ba mẹ Johny bạo lực và vụ tự sát tương tự |
| `25_phap-y.md` | 10 | Kết quả khám nghiệm thi thể Chris và Danie |
| `26_jack.md` | 5 | Bạn thân cặp đôi, chủ tiệm sửa xe, chuyến thăm tối hôm trước án mạng |

## Cách nạp vào RAG

Luồng đầy đủ cho cả hai nhánh nằm ở [README.md](README.md). Tóm tắt:

1. **Chunking:** một dòng trong thân file là một chunk. Tách bằng ký tự xuống dòng, bỏ dòng
   rỗng và bỏ khối frontmatter. Không cần splitter theo token hay theo heading.
2. **Metadata:** copy toàn bộ frontmatter xuống từng chunk. Trường `loai` quyết định
   `trang_thai` của cạnh trong đồ thị, nên không được bỏ.
3. **Mã chunk:** sinh theo công thức `<doc_id>#L<số thứ tự dòng>`. Tài liệu không chứa mã.
4. **Extract entity và relation:** dùng LLM theo `graphrag/graph-schema.json`, chạy từng dòng
   một. Đưa `graphrag/entities-by-doc.json` vào prompt làm gợi ý, và chuẩn hóa tên theo
   bảng trong `graphrag/ONTOLOGY.md` trước khi `MERGE`.
5. **Lọc theo nhân vật:** dùng trường `scene` và `nguon` trong frontmatter, hoặc bảng "Nhân
   vật dùng file nào" ở trên, để giới hạn tri thức mỗi NPC truy xuất được.

Chi tiết và code mẫu nằm ở [graphrag/SCHEMA.md §4](graphrag/SCHEMA.md).

## Bản đồ mâu thuẫn giữa các lời khai

Đây là xương sống suy luận của vụ án, dùng để kiểm tra đồ thị sau khi build — mỗi ô là một
cạnh `UNG_HO` hoặc `MAU_THUAN` tới node `:GiaThuyet` tương ứng:

Cột trái là `ten` của node `:GiaThuyet` trong đồ thị, viết đúng như trong
[graphrag/entities.json](graphrag/entities.json).

| Giả thuyết (`:GiaThuyet`) | Ủng hộ | Mâu thuẫn |
|---|---|---|
| Giả thuyết giết người rồi tự sát | Cảnh sát, pháp y (dấu cầm súng tay trái) | Pháp y (không chắc Chris bắn Danie), Jack, Wills (15 phút), tấm ảnh chiếc xe |
| Giả thuyết cặp đôi Chris–Danie bất hòa | Tin đồn tiếng la hét, tin đồn cãi nhau, ông lão tiệm bánh mì | Jack, Brad |
| Giả thuyết Chris mất kiểm soát | Tin đồn Chris đánh Danie, Wills | Brad, Ông lão tiệm bánh mì |
| Giả thuyết Chris tự bắn mình | Pháp y (vết đạn trên trán, dấu cầm súng tay trái) | Jack |
| Giả thuyết Danie tự sát hoặc chống cự | — | Pháp y (không dấu cầm súng, không dấu ẩu đả) |
| Giả thuyết có người thứ ba tại hiện trường | Pháp y, Jack, bà Johny, bộ ảnh của Johny | — |
| Giả thuyết Johny theo dõi cặp đôi Chris–Danie | Bộ ảnh ngôi nhà, tấm ảnh ghi lời nói | — |

Đây chính là kết quả truy vấn 5.4 trong `graphrag/SCHEMA.md`. Nếu sau khi build mà truy vấn
đó không trả về đủ các cặp này, đồ thị đang thiếu cạnh lập luận — thường là do LLM extract bỏ
sót `:NhanDinh`. Bộ cạnh chuẩn nằm sẵn trong
[graphrag/relationships.json](graphrag/relationships.json).

> "Vụ án là án đóng kín" không còn là một node riêng — nó chính là mặt sau của *Giả thuyết
> giết người rồi tự sát*, nên mọi bằng chứng về người thứ ba đều treo vào giả thuyết đó. Cửa
> sổ mở ở Scene 1 chưa có trong `corpus/`, nên chưa có cạnh tương ứng.

---

# Thông tin không thuộc nhân vật nào

Phần dưới đây là vật chứng, cơ chế và luồng game, không phải lời khai của NPC, nên không
nằm trong các file trên.

## Cơ chế sổ tay

Các đối tượng người chơi tương tác được sẽ tự động thêm vào quyển sổ tay, kèm một dòng như
dòng suy nghĩ của nhân vật chính. Các câu gợi ý có thể chỉ dẫn người chơi làm gì để tìm
thêm hint.

Lời khai quan trọng của NPC cũng được tự động bổ sung vào sổ tay kèm dòng suy nghĩ.

Các object tương tác được đều có dấu hiệu nhận biết cho người chơi.

## Scene 1 — Phòng ăn nhà nạn nhân và khu vực đường trước cửa

Vật chứng tại hiện trường:

| Vật chứng | Mô tả | Dòng suy nghĩ trong sổ tay |
|---|---|---|
| Cửa sổ | Một chiếc cửa sổ bị mở toang ngay từ bên ngoài ngôi nhà | "Tại sao cửa sổ này lại mở, hiện trường không phải một vụ án đóng kín." |
| Hai thi thể | Cặp đôi nằm gục trên vũng máu ở sàn nhà | — |
| Khẩu súng | Nằm lăn lóc trên sàn, gần với thi thể của Chris | "Số đạn trong súng còn lại thật sự khớp với vụ án (2 viên đạn đã bắn)" |
| Rượu vang | Rượu vang đỏ và hai chiếc ly đặt gần khu vực ngăn kéo phía trên thi thể Chris | "Trước đó họ đã có 1 buổi tối lãng mạn sao, tại sao lại tự sát" |

Dựa vào việc cửa sổ là một clue, người chơi có thể suy luận ra có kẻ thứ 3 đột nhập.

NPC trong scene: 2 cảnh sát và Wills.

## Scene 2 — Đường đi khu phố gần chỗ vụ án

Chuyển cảnh: mọi người đi về và giải tán, sáng hôm sau thám tử đi trên đường gần đó để
điều tra tiếp vụ án. Trên đường đi sẽ có nhiều NPC qua lại, trong đó một vài NPC tương tác
được với vai trò người dân.

Brad ngồi ở một góc trong một quán cafe và sẽ chủ động bắt chuyện khi nhìn thấy người chơi,
vì đã đến hiện trường vụ án và nhìn thấy người chơi.

Jack đứng ở tiệm sửa xe của Jack trên cùng con đường. Jack là người gần nhất được biết đã
gặp Chris và Danie trước khi vụ án xảy ra, nên lời khai của Jack là đối trọng với tin đồn
của hàng xóm về việc cặp đôi bất hòa.

Khi tương tác với Johny sẽ có sự kiện các tấm ảnh của nhân vật này bay ra. Người chơi nhặt
lại giúp Johny và phát hiện tấm hình một chiếc xe đậu trước nhà Chris. Tấm hình này được
thêm vào sổ tay.

Sau khi người chơi hỏi hết, người chơi chọn quay về nhà nghỉ ngơi và sáng hôm sau trở lại
hiện trường điều tra phòng ngủ của cặp đôi.

### Mắt xích chiếc xe (điểm thiết kế cần chốt)

Tấm ảnh chiếc xe (trong `23_johny.md`) và chuyến thăm của Jack (trong `26_jack.md`) đang
chạm vào nhau. Có hai hướng, cần chọn một:

- **Hướng A — Jack là red herring:** chiếc xe trong ảnh chính là xe của Jack. Người chơi
  nghi Jack, hỏi Jack, Jack giải thích là đến sửa xe vào *tối hôm trước*, ngõ cụt đóng lại
  và người chơi phải quay sang manh mối khác.
- **Hướng B — hai chiếc xe khác nhau:** xe của Jack ở tối hôm trước, còn chiếc xe trong ảnh
  chụp vào đêm án mạng, vẫn là dấu vết của kẻ thứ ba. Lúc này Jack chỉ có vai trò phá vỡ
  giả thuyết cặp đôi bất hòa.

Nếu chọn hướng A, cần bổ sung vào `corpus/26_jack.md` một dòng mô tả chiếc xe của Jack (màu, loại)
để người chơi đối chiếu được với tấm ảnh.

## Scene 3 — Phòng ngủ của nạn nhân

Phòng ngủ có 1 bàn trang điểm, 1 giường đôi, 1 đèn ngủ và một số object khác.

| Vật chứng | Ghi chú trong sổ tay |
|---|---|
| Quyển sách "The Dreamer's Guide to the World", sách tích cực về du lịch, có ghi chú chữ viết tay trên trang đầu: "Gửi Danie, đừng bao giờ từ bỏ giấc mơ. Love, Chris" | Thêm vào sổ tay |
| Ảnh đôi của Chris và Danie | Không thêm vào |
| Chiếc giường không được dọn dẹp | Thêm vào sổ tay |
| Một chiếc bảng với hình của Chris, Danie và một người lạ mặt | "Người này là ai? Có quan hệ gì với Chris và Danie?" |
| Một bảng tính toán tài chính với kế hoạch khá lớn | Thêm vào sổ tay |
| Một danh sách ghim trên bảng: "Madagascar, Nam Cực, Ukraine, ??, ??", dưới danh sách có chữ viết tay của người khác "Chỉ còn 1 nửa thế giới", được gạch chân 2 lần | Thêm vào sổ tay |

Sau khi tương tác toàn bộ và người chơi đi ra khỏi phòng, người chơi được chuyển cảnh di
chuyển đến phòng của pháp y để xem thông tin từ tử thi.

## Scene 4 — Phòng khám nghiệm tử thi

Người chơi có thể tương tác với pháp y để hỏi các câu hỏi về tử thi.

## Scene 5 — Nhà Johny

Người chơi tự do di chuyển trong scene này. Nhà Johny ở ngay trên đường trong Scene 2.
Scene 2 lúc này chỉ còn các NPC biết về thông tin vụ án nhưng không biết rõ.

Người chơi chuyển cảnh đến nhà Johny khi tương tác với nhà để hỏi thăm về tấm hình trước
đó, và gặp bà Johny trước.

Trong nhà:

- Trên tường có treo hình gia đình Johny gồm đủ ba mẹ và Johny, nhưng họ không có ở nhà.
- Có một tờ báo về lịch chiếu một trận bóng, trận đấu kết thúc vào 30 phút trước khi vụ án
  xảy ra.

Bà Johny dắt người chơi vào phòng Johny. Trong phòng có:

- Đầy những tấm ảnh về khung cảnh ngôi nhà của Chris như đang theo dõi họ.
- Một tấm ảnh rớt ra, ghi rõ những lời Chris và Danie nói chuyện với nhau.
- Một đôi găng tay trên bàn của Johny.

Các đối tượng này sẽ được thêm vào sổ tay khi được tương tác.

## Điều kiện thắng

Người chơi kết luận được Johny là thủ phạm nếu tinh ý phát hiện và chiến thắng.
