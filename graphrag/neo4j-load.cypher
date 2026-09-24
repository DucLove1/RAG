// =====================================================================================
// graphrag/neo4j-load.cypher  -  du lieu do thi, sinh tu graphrag/*.json.
//
// KHONG SUA FILE NAY BANG TAY. Nguon su that la ontology.json / entities.json /
// relationships.json; file nay chi la mot cach dua chung len Neo4j khi khong chay app.
// Duong con lai la POST /api/graph/load, va no con ghi ca phan quyen NPC.
//
//     cypher-shell -a <uri> -u <user> -p <pass> -f neo4j-load.cypher
//
// MERGE-only: chay lai bao nhieu lan cung ra mot ket qua, va khong xoa gi. Doi lai, mot
// canh da go khoi file nguon van con trong DB cho toi khi co nguoi xoa tay.
//
// Chay truoc neo4j-init.cypher. Neu database dang co do thi cu cua mot ban khac, xoa
// sach truoc - chi du lieu, schema khong bi anh huong:
//     MATCH (n) DETACH DELETE n;
// =====================================================================================

// ---------- 118 thuc the ----------

UNWIND [
  {ten: "Kỳ dị", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"]}},
  {ten: "Bạn trai tồi tệ", props: {nguon_chunk: ["20_wills#L2"]}},
  {ten: "Bạo lực", props: {nguon_chunk: ["20_wills#L2"]}},
  {ten: "Tham vọng", props: {nguon_chunk: ["21_brad#L3"]}},
  {ten: "Lạc quan", props: {nguon_chunk: ["21_brad#L3"]}},
  {ten: "Có biểu hiện bất thường", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"]}},
  {ten: "Bình tĩnh", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"]}},
  {ten: "Đầy năng lượng", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"]}},
  {ten: "Tích cực", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"]}},
  {ten: "Dấu ẩu đả", props: {nguon_chunk: ["25_phap-y#L3"]}},
  {ten: "Nồng độ cồn cao", props: {nguon_chunk: ["25_phap-y#L5", "25_phap-y#L9"]}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:DacDiem
SET e += row.props;

UNWIND [
  {ten: "Ngôi nhà nạn nhân", props: {nguon_chunk: ["00_boi-canh-chung#L2", "00_boi-canh-chung#L4", "11_chung_tin-don-tieng-la-het#L1", "12_chung_tin-don-khoa-minh-trong-nha#L2", "22_ong-lao-tiem-banh-mi#L3", "23_johny#L3", "23_johny#L4", "26_jack#L3"], bi_danh: ["nhà của Chris và Danie", "hiện trường"]}},
  {ten: "Phòng ăn", props: {nguon_chunk: ["00_boi-canh-chung#L2"]}},
  {ten: "Phòng khách", props: {nguon_chunk: ["00_boi-canh-chung#L2"]}},
  {ten: "Nhà bếp", props: {nguon_chunk: ["00_boi-canh-chung#L2"]}},
  {ten: "Khu phố", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L1", "22_ong-lao-tiem-banh-mi#L3"]}},
  {ten: "Nhà của Bread", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L1"]}},
  {ten: "Tiệm bánh mì của Bread", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], bi_danh: ["tiệm bánh mì"]}},
  {ten: "Trường cấp 3", props: {nguon_chunk: ["21_brad#L1"]}},
  {ten: "Trường đại học", props: {nguon_chunk: ["21_brad#L1"]}},
  {ten: "Nhà Johny", props: {nguon_chunk: ["23_johny#L1", "23_johny#L6", "24_ba-johny#L1"]}},
  {ten: "Phòng Johny", props: {nguon_chunk: ["23_johny#L6"]}},
  {ten: "Tiệm sửa xe của Jack", props: {nguon_chunk: ["26_jack#L2"]}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:DiaDiem
SET e += row.props;

UNWIND [
  {ten: "Giả thuyết giết người rồi tự sát", props: {nguon_chunk: ["00_boi-canh-chung#L4", "10_chung_canh-sat#L1", "20_wills#L1", "25_phap-y#L10", "25_phap-y#L8", "26_jack#L5"], trang_thai: "dang_mo"}},
  {ten: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L2", "12_chung_tin-don-khoa-minh-trong-nha#L2", "21_brad#L4", "22_ong-lao-tiem-banh-mi#L3", "22_ong-lao-tiem-banh-mi#L4", "26_jack#L4"], trang_thai: "dang_mo"}},
  {ten: "Giả thuyết Chris mất kiểm soát", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2", "20_wills#L2", "21_brad#L3", "22_ong-lao-tiem-banh-mi#L5"], trang_thai: "dang_mo"}},
  {ten: "Giả thuyết Chris tự bắn mình", props: {nguon_chunk: ["25_phap-y#L6", "25_phap-y#L8", "26_jack#L5"], trang_thai: "dang_mo"}},
  {ten: "Giả thuyết Danie tự sát hoặc chống cự", props: {nguon_chunk: ["25_phap-y#L3"], trang_thai: "dang_mo"}},
  {ten: "Giả thuyết có người thứ ba tại hiện trường", props: {nguon_chunk: ["23_johny#L4", "24_ba-johny#L3", "25_phap-y#L10", "26_jack#L5"], trang_thai: "dang_mo"}},
  {ten: "Giả thuyết Johny theo dõi cặp đôi Chris–Danie", props: {nguon_chunk: ["23_johny#L3", "23_johny#L5"], trang_thai: "dang_mo"}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:GiaThuyet
SET e += row.props;

UNWIND [
  {ten: "Chris", props: {nguon_chunk: ["00_boi-canh-chung#L1", "10_chung_canh-sat#L1", "12_chung_tin-don-khoa-minh-trong-nha#L1", "12_chung_tin-don-khoa-minh-trong-nha#L2", "20_wills#L2", "21_brad#L1", "21_brad#L3", "22_ong-lao-tiem-banh-mi#L3", "22_ong-lao-tiem-banh-mi#L4", "22_ong-lao-tiem-banh-mi#L5", "23_johny#L5", "25_phap-y#L6", "25_phap-y#L8", "25_phap-y#L9", "26_jack#L1"], gioi_tinh: "nam", vai_tro: ["nan_nhan"]}},
  {ten: "Danie", props: {nguon_chunk: ["00_boi-canh-chung#L1", "12_chung_tin-don-khoa-minh-trong-nha#L1", "12_chung_tin-don-khoa-minh-trong-nha#L2", "21_brad#L2", "22_ong-lao-tiem-banh-mi#L5", "23_johny#L5", "25_phap-y#L2", "25_phap-y#L3", "25_phap-y#L5", "26_jack#L1"], gioi_tinh: "nu", vai_tro: ["nan_nhan"]}},
  {ten: "James", props: {nguon_chunk: ["10_chung_canh-sat#L1"], bi_danh: ["Cảnh sát 1"], gioi_tinh: "nam", vai_tro: ["chuyen_mon"], nghe_nghiep: "cảnh sát"}},
  {ten: "Michael", props: {nguon_chunk: ["10_chung_canh-sat#L1"], bi_danh: ["Cảnh sát 2"], gioi_tinh: "nam", vai_tro: ["chuyen_mon"], nghe_nghiep: "cảnh sát"}},
  {ten: "Wills", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1", "20_wills#L1", "20_wills#L2"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Daniel", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], bi_danh: ["Người đi đường 1"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Thomas", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], bi_danh: ["Người đi đường 2"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Arthur", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], bi_danh: ["Người dân ngồi bên đường 1"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Clara", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], bi_danh: ["Người dân ngồi bên đường 2"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Brad", props: {nguon_chunk: ["21_brad#L1", "21_brad#L2", "21_brad#L3", "21_brad#L4"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Bread", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L1", "22_ong-lao-tiem-banh-mi#L2", "22_ong-lao-tiem-banh-mi#L3", "22_ong-lao-tiem-banh-mi#L4", "22_ong-lao-tiem-banh-mi#L5"], bi_danh: ["Ông lão tiệm bánh mì"], gioi_tinh: "nam", vai_tro: ["nhan_chung"], nghe_nghiep: "chủ tiệm bánh mì"}},
  {ten: "Johny", props: {nguon_chunk: ["23_johny#L1", "23_johny#L3", "23_johny#L4", "23_johny#L5", "23_johny#L6", "24_ba-johny#L1", "24_ba-johny#L2"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"]}},
  {ten: "Margaret", props: {nguon_chunk: ["24_ba-johny#L1", "24_ba-johny#L2", "24_ba-johny#L3"], bi_danh: ["Bà Johny"], gioi_tinh: "nu", vai_tro: ["nguoi_than"]}},
  {ten: "Edward", props: {nguon_chunk: ["25_phap-y#L1", "25_phap-y#L10", "25_phap-y#L3", "25_phap-y#L4", "25_phap-y#L7"], bi_danh: ["Pháp y"], gioi_tinh: "khong_ro", vai_tro: ["chuyen_mon"], nghe_nghiep: "pháp y"}},
  {ten: "Jack", props: {nguon_chunk: ["26_jack#L1", "26_jack#L2", "26_jack#L3", "26_jack#L4", "26_jack#L5"], gioi_tinh: "khong_ro", vai_tro: ["nhan_chung"], nghe_nghiep: "sửa xe"}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:Nguoi
SET e += row.props;

UNWIND [
  {ten: "Chris giết Danie rồi sau đó Chris tự sát", props: {nguon_chunk: ["10_chung_canh-sat#L1"], nguoi_khai: "Cảnh sát", do_tin_cay: "trung_binh", loai: "ket_luan_chuyen_mon"}},
  {ten: "Cảnh sát không có thêm thông tin nào khác về vụ án", props: {nguon_chunk: ["10_chung_canh-sat#L2"], nguoi_khai: "Cảnh sát", do_tin_cay: "cao", loai: "ket_luan_chuyen_mon"}},
  {ten: "Thường xuyên có tiếng la hét và đập phá từ ngôi nhà nạn nhân", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L2"], nguoi_khai: "Người dân khu phố", do_tin_cay: "trung_binh", loai: "tin_don"}},
  {ten: "Chris và Danie là những người kỳ dị hay khóa mình trong nhà", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], nguoi_khai: "Người dân khu phố", do_tin_cay: "thap", loai: "tin_don"}},
  {ten: "Chris và Danie khóa mình trong nhà vì cãi nhau", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], nguoi_khai: "Người dân khu phố", do_tin_cay: "thap", loai: "tin_don"}},
  {ten: "Chris từng đánh Danie", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], nguoi_khai: "Người dân khu phố", do_tin_cay: "thap", loai: "tin_don"}},
  {ten: "Có hai tiếng súng cách nhau 15 phút trong đêm án mạng", props: {nguon_chunk: ["20_wills#L1"], nguoi_khai: "Wills", do_tin_cay: "cao", loai: "quan_sat_truc_tiep"}},
  {ten: "Chris là một gã bạn trai tồi tệ và bạo lực", props: {nguon_chunk: ["20_wills#L2"], nguoi_khai: "Wills", do_tin_cay: "thap", loai: "suy_doan"}},
  {ten: "Chris là người có tham vọng và lạc quan", props: {nguon_chunk: ["21_brad#L3"], nguoi_khai: "Brad", do_tin_cay: "trung_binh", loai: "suy_doan"}},
  {ten: "Chris không dễ bị mất kiểm soát", props: {nguon_chunk: ["21_brad#L3"], nguoi_khai: "Brad", do_tin_cay: "trung_binh", loai: "suy_doan"}},
  {ten: "Chris và Danie đang yêu nhau sâu đậm", props: {nguon_chunk: ["21_brad#L4"], nguoi_khai: "Brad", do_tin_cay: "trung_binh", loai: "suy_doan"}},
  {ten: "Chris có điều bất thường trong những lần cuối đến tiệm bánh mì", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"], nguoi_khai: "Bread", do_tin_cay: "trung_binh", loai: "quan_sat_truc_tiep"}},
  {ten: "Có thể Chris đang có vấn đề với Danie", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"], nguoi_khai: "Bread", do_tin_cay: "thap", loai: "suy_doan"}},
  {ten: "4–5 tháng trước Chris khá bình tĩnh còn Danie đầy năng lượng và tích cực", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], nguoi_khai: "Bread", do_tin_cay: "trung_binh", loai: "quan_sat_truc_tiep"}},
  {ten: "Johny ở nhà khi vụ án xảy ra", props: {nguon_chunk: ["23_johny#L1"], nguoi_khai: "Johny", do_tin_cay: "thap", loai: "quan_sat_truc_tiep"}},
  {ten: "Ba mẹ Johny thường xuyên cãi nhau và bạo lực khi Johny còn bé", props: {nguon_chunk: ["24_ba-johny#L2"], nguoi_khai: "Margaret", do_tin_cay: "trung_binh", loai: "quan_sat_truc_tiep"}},
  {ten: "Vụ tự sát của ba mẹ Johny tương tự hiện trường vụ án Chris–Danie", props: {nguon_chunk: ["24_ba-johny#L3"], nguoi_khai: "Margaret", do_tin_cay: "trung_binh", loai: "quan_sat_truc_tiep"}},
  {ten: "Danie bị bắn ở khoảng cách gần dưới 10cm", props: {nguon_chunk: ["25_phap-y#L1"], nguoi_khai: "Edward", do_tin_cay: "cao", loai: "ket_luan_chuyen_mon"}},
  {ten: "Trên tay Danie không có dấu ẩu đả và không có dấu hiệu cầm súng", props: {nguon_chunk: ["25_phap-y#L3"], nguoi_khai: "Edward", do_tin_cay: "cao", loai: "ket_luan_chuyen_mon"}},
  {ten: "Dấu vân tay có thể tiết lộ thêm thông tin", props: {nguon_chunk: ["25_phap-y#L3"], nguoi_khai: "Edward", do_tin_cay: "trung_binh", loai: "suy_doan"}},
  {ten: "Có thể ai đó hoặc chính Danie đã dùng lưỡi dao gây tổn thương cho Danie nhiều lần", props: {nguon_chunk: ["25_phap-y#L4"], nguoi_khai: "Edward", do_tin_cay: "thap", loai: "suy_doan"}},
  {ten: "Vết vỡ thứ hai trên hộp sọ Chris là vết đạn bay ra", props: {nguon_chunk: ["25_phap-y#L7"], nguoi_khai: "Edward", do_tin_cay: "trung_binh", loai: "suy_doan"}},
  {ten: "Không chắc chắn Chris là người bắn Danie", props: {nguon_chunk: ["25_phap-y#L10"], nguoi_khai: "Edward", do_tin_cay: "cao", loai: "ket_luan_chuyen_mon"}},
  {ten: "Tối hôm trước án mạng Chris và Danie vẫn hòa thuận", props: {nguon_chunk: ["26_jack#L4"], nguoi_khai: "Jack", do_tin_cay: "cao", loai: "quan_sat_truc_tiep"}},
  {ten: "Chris và Danie không tự sát", props: {nguon_chunk: ["26_jack#L5"], nguoi_khai: "Jack", do_tin_cay: "trung_binh", loai: "suy_doan"}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:NhanDinh
SET e += row.props;

UNWIND [
  {ten: "Cặp đôi Chris–Danie", props: {nguon_chunk: ["00_boi-canh-chung#L1", "00_boi-canh-chung#L2", "00_boi-canh-chung#L4", "12_chung_tin-don-khoa-minh-trong-nha#L1", "26_jack#L3"], bi_danh: ["cặp đôi", "Chris và Danie"]}},
  {ten: "Cảnh sát", props: {nguon_chunk: ["10_chung_canh-sat#L1", "10_chung_canh-sat#L2"]}},
  {ten: "Người dân khu phố", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1", "11_chung_tin-don-tieng-la-het#L2", "12_chung_tin-don-khoa-minh-trong-nha#L1", "12_chung_tin-don-khoa-minh-trong-nha#L2"], bi_danh: ["hàng xóm"]}},
  {ten: "Gia đình Johny", props: {nguon_chunk: ["24_ba-johny#L1", "24_ba-johny#L2"]}},
  {ten: "Ba mẹ Johny", props: {nguon_chunk: ["24_ba-johny#L2", "24_ba-johny#L3"]}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:Nhom
SET e += row.props;

UNWIND [
  {ten: "Vụ án Chris–Danie", props: {nguon_chunk: ["00_boi-canh-chung#L1", "00_boi-canh-chung#L2", "00_boi-canh-chung#L3", "10_chung_canh-sat#L1", "24_ba-johny#L3", "25_phap-y#L3"], bi_danh: ["vụ án", "án mạng"]}},
  {ten: "Tiếng la hét", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"]}},
  {ten: "Tiếng đập phá", props: {nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"]}},
  {ten: "Cãi nhau giữa Chris và Danie", props: {nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"]}},
  {ten: "Tiếng súng thứ nhất", props: {nguon_chunk: ["20_wills#L1"]}},
  {ten: "Tiếng súng thứ hai", props: {nguon_chunk: ["20_wills#L1"]}},
  {ten: "Chris chuyển tới khu phố", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"]}},
  {ten: "Chris ngừng đến tiệm bánh mì", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"]}},
  {ten: "Trận bóng", props: {nguon_chunk: ["23_johny#L2"]}},
  {ten: "Xung đột bạo lực của ba mẹ Johny", props: {nguon_chunk: ["24_ba-johny#L2"]}},
  {ten: "Vụ tự sát của ba mẹ Johny", props: {nguon_chunk: ["24_ba-johny#L3"]}},
  {ten: "Danie bị bắn", props: {nguon_chunk: ["25_phap-y#L1", "25_phap-y#L2"]}},
  {ten: "Chris bị bắn vào đầu", props: {nguon_chunk: ["25_phap-y#L6"]}},
  {ten: "Chuyến thăm của Jack tối hôm trước án mạng", props: {nguon_chunk: ["26_jack#L3"]}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:SuKien
SET e += row.props;

UNWIND [
  {ten: "Đêm xảy ra án mạng", props: {nguon_chunk: ["00_boi-canh-chung#L3", "11_chung_tin-don-tieng-la-het#L2", "20_wills#L1", "22_ong-lao-tiem-banh-mi#L2", "23_johny#L1"], thu_tu: "6"}},
  {ten: "Thời điểm xảy ra vụ án", props: {nguon_chunk: ["00_boi-canh-chung#L3", "23_johny#L1", "25_phap-y#L5"], thu_tu: "8"}},
  {ten: "Thời điểm 2 phút trước vụ án", props: {nguon_chunk: ["23_johny#L1"], thu_tu: "7"}},
  {ten: "Mốc 15 phút sau tiếng súng thứ nhất", props: {nguon_chunk: ["20_wills#L1"], thu_tu: "9"}},
  {ten: "Tối hôm trước án mạng", props: {nguon_chunk: ["26_jack#L3"], thu_tu: "5"}},
  {ten: "Dạo gần đây", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], thu_tu: "4"}},
  {ten: "Mốc 4–5 tháng trước", props: {nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], thu_tu: "3"}},
  {ten: "Thời thơ ấu của Johny", props: {nguon_chunk: ["24_ba-johny#L2"], thu_tu: "2"}},
  {ten: "Nhiều năm trước", props: {nguon_chunk: ["25_phap-y#L4"], thu_tu: "1"}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:ThoiGian
SET e += row.props;

UNWIND [
  {ten: "Thi thể Chris", props: {nguon_chunk: ["00_boi-canh-chung#L2", "25_phap-y#L6", "25_phap-y#L7", "25_phap-y#L8"], trong_so_tay: "True"}},
  {ten: "Thi thể Danie", props: {nguon_chunk: ["00_boi-canh-chung#L2", "25_phap-y#L1", "25_phap-y#L2", "25_phap-y#L3", "25_phap-y#L4"], trong_so_tay: "True"}},
  {ten: "Vũng máu tại hiện trường", props: {nguon_chunk: ["00_boi-canh-chung#L2"], trong_so_tay: "False"}},
  {ten: "Khẩu súng", props: {nguon_chunk: ["25_phap-y#L1", "25_phap-y#L2", "25_phap-y#L3", "25_phap-y#L6", "25_phap-y#L8"], trong_so_tay: "True"}},
  {ten: "Tấm ảnh ngoại phạm của Johny", props: {nguon_chunk: ["23_johny#L1", "23_johny#L2"], trong_so_tay: "True"}},
  {ten: "Đồng hồ trong tấm ảnh ngoại phạm", props: {nguon_chunk: ["23_johny#L1"], trong_so_tay: "False"}},
  {ten: "Bộ ảnh chụp ngôi nhà nạn nhân", props: {nguon_chunk: ["23_johny#L3"], trong_so_tay: "True"}},
  {ten: "Tấm ảnh chiếc xe", props: {nguon_chunk: ["23_johny#L4"], trong_so_tay: "True"}},
  {ten: "Chiếc xe đậu trước ngôi nhà nạn nhân", props: {nguon_chunk: ["23_johny#L4"], trong_so_tay: "True"}},
  {ten: "Tấm ảnh ghi lời nói của Chris và Danie", props: {nguon_chunk: ["23_johny#L5"], trong_so_tay: "True"}},
  {ten: "Đôi găng tay", props: {nguon_chunk: ["23_johny#L6"], trong_so_tay: "True"}},
  {ten: "Vết cháy và tóc cháy trên thi thể Danie", props: {nguon_chunk: ["25_phap-y#L1"], trong_so_tay: "True"}},
  {ten: "Vết đạn xuyên bụng Danie", props: {nguon_chunk: ["25_phap-y#L2"], trong_so_tay: "True"}},
  {ten: "Dấu vân tay", props: {nguon_chunk: ["25_phap-y#L3"], trong_so_tay: "True"}},
  {ten: "Vết thương cũ trên tay Danie", props: {nguon_chunk: ["25_phap-y#L4"], trong_so_tay: "True"}},
  {ten: "Lưỡi dao", props: {nguon_chunk: ["25_phap-y#L4"], trong_so_tay: "False"}},
  {ten: "Vết đạn trên trán Chris", props: {nguon_chunk: ["25_phap-y#L6"], trong_so_tay: "True"}},
  {ten: "Vết vỡ hộp sọ bên trái đầu Chris", props: {nguon_chunk: ["25_phap-y#L7"], trong_so_tay: "True"}},
  {ten: "Dấu vết cầm súng trên tay trái Chris", props: {nguon_chunk: ["25_phap-y#L8"], trong_so_tay: "True"}},
  {ten: "Xe của cặp đôi Chris–Danie", props: {nguon_chunk: ["26_jack#L3"], trong_so_tay: "False"}}
] AS row
MERGE (e:ThucThe {ten: row.ten})
SET e:VatChung
SET e += row.props;


// ---------- 179 canh (loai doi xung khai MOT chieu, duoi day phat ca hai) ----------

UNWIND [
  {tu: "Chris", den: "Danie", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:BAO_LUC_VOI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Người dân khu phố", den: "Tiếng la hét", props: {trang_thai: "tin_don", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false, giac_quan: "thính giác"}},
  {tu: "Người dân khu phố", den: "Tiếng đập phá", props: {trang_thai: "tin_don", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false, giac_quan: "thính giác"}},
  {tu: "Wills", den: "Tiếng súng thứ nhất", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false, giac_quan: "thính giác"}},
  {tu: "Wills", den: "Tiếng súng thứ hai", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false, giac_quan: "thính giác"}},
  {tu: "Jack", den: "Chuyến thăm của Jack tối hôm trước án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L3"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CHUNG_KIEN]->(b)
SET r += row.props;

UNWIND [
  {tu: "Bread", den: "Đêm xảy ra án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L2"], phu_dinh: true, mo_ta: "Vào thời gian xảy ra án mạng thì Bread đã đi ngủ, không ai xác nhận được."}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_CHUNG_CU_NGOAI_PHAM]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris", den: "Kỳ dị", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], phu_dinh: false}},
  {tu: "Danie", den: "Kỳ dị", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], phu_dinh: false}},
  {tu: "Chris", den: "Bạn trai tồi tệ", props: {trang_thai: "suy_doan", nguon_chunk: ["20_wills#L2"], phu_dinh: false}},
  {tu: "Chris", den: "Bạo lực", props: {trang_thai: "suy_doan", nguon_chunk: ["20_wills#L2"], phu_dinh: false}},
  {tu: "Chris", den: "Tham vọng", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L3"], phu_dinh: false}},
  {tu: "Chris", den: "Lạc quan", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L3"], phu_dinh: false}},
  {tu: "Chris", den: "Có biểu hiện bất thường", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"], phu_dinh: false}},
  {tu: "Chris", den: "Bình tĩnh", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], phu_dinh: false}},
  {tu: "Danie", den: "Đầy năng lượng", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], phu_dinh: false}},
  {tu: "Danie", den: "Tích cực", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], phu_dinh: false}},
  {tu: "Danie", den: "Nồng độ cồn cao", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L5"], phu_dinh: false}},
  {tu: "Chris", den: "Nồng độ cồn cao", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L9"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_DAC_DIEM]->(b)
SET r += row.props;

UNWIND [
  {tu: "Ngôi nhà nạn nhân", den: "Giả thuyết giết người rồi tự sát", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L4"], phu_dinh: false, mo_ta: "Hiện trường mang dấu hiệu của một vụ giết người rồi tự sát."}},
  {tu: "Chris ngừng đến tiệm bánh mì", den: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {trang_thai: "suy_doan", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Thi thể Danie", den: "Vết cháy và tóc cháy trên thi thể Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L1"], phu_dinh: false}},
  {tu: "Thi thể Danie", den: "Vết đạn xuyên bụng Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L2"], phu_dinh: false}},
  {tu: "Thi thể Danie", den: "Dấu ẩu đả", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L3"], phu_dinh: true}},
  {tu: "Thi thể Danie", den: "Vết thương cũ trên tay Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L4"], phu_dinh: false}},
  {tu: "Thi thể Chris", den: "Vết đạn trên trán Chris", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L6"], phu_dinh: false}},
  {tu: "Thi thể Chris", den: "Vết vỡ hộp sọ bên trái đầu Chris", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L7"], phu_dinh: false}},
  {tu: "Thi thể Chris", den: "Dấu vết cầm súng trên tay trái Chris", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L8"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_DAU_HIEU]->(b)
SET r += row.props;

UNWIND [
  {tu: "Danie", den: "Khẩu súng", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L3"], phu_dinh: true}},
  {tu: "Chris", den: "Khẩu súng", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L8"], phu_dinh: false, vi_tri: "tay trái"}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_DAU_VET_CAM]->(b)
SET r += row.props;

UNWIND [
  {tu: "Brad", den: "Trường cấp 3", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L1"], phu_dinh: false}},
  {tu: "Chris", den: "Trường cấp 3", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L1"], phu_dinh: false}},
  {tu: "Brad", den: "Trường đại học", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L1"], phu_dinh: false}},
  {tu: "Chris", den: "Trường đại học", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L1"], phu_dinh: false}},
  {tu: "Johny", den: "Nhà Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}},
  {tu: "Chiếc xe đậu trước ngôi nhà nạn nhân", den: "Ngôi nhà nạn nhân", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L4"], phu_dinh: false}},
  {tu: "Jack", den: "Ngôi nhà nạn nhân", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L3"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_MAT_TAI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris", den: "Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}},
  {tu: "Danie", den: "Chris", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_QUAN_HE_TINH_CAM]->(b)
SET r += row.props;

UNWIND [
  {tu: "Người dân khu phố", den: "Cặp đôi Chris–Danie", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], phu_dinh: false, thai_do: "chủ động tránh xa"}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:CO_THAI_DO_VOI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Thời điểm xảy ra vụ án", den: "Vụ án Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L3"], phu_dinh: false}},
  {tu: "Mốc 15 phút sau tiếng súng thứ nhất", den: "Tiếng súng thứ hai", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false}},
  {tu: "Mốc 4–5 tháng trước", den: "4–5 tháng trước Chris khá bình tĩnh còn Danie đầy năng lượng và tích cực", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], phu_dinh: false}},
  {tu: "Thời điểm 2 phút trước vụ án", den: "Tấm ảnh ngoại phạm của Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:DANH_DAU]->(b)
SET r += row.props;

UNWIND [
  {tu: "Thi thể Chris", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Thi thể Danie", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Vũng máu tại hiện trường", den: "Phòng ăn", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Vũng máu tại hiện trường", den: "Phòng khách", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Đôi găng tay", den: "Phòng Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L6"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:DUOC_TIM_THAY_TAI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris", den: "Vụ án Chris–Danie", props: {trang_thai: "suy_doan", nguon_chunk: ["10_chung_canh-sat#L1"], phu_dinh: false, theo_loi_khai_cua: "Cảnh sát"}},
  {tu: "Chris", den: "Cãi nhau giữa Chris và Danie", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Danie", den: "Cãi nhau giữa Chris và Danie", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Ba mẹ Johny", den: "Xung đột bạo lực của ba mẹ Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L2"], phu_dinh: false}},
  {tu: "Ba mẹ Johny", den: "Vụ tự sát của ba mẹ Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L3"], phu_dinh: false}},
  {tu: "Khẩu súng", den: "Danie bị bắn", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L1", "25_phap-y#L2"], phu_dinh: false}},
  {tu: "Khẩu súng", den: "Chris bị bắn vào đầu", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L6"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:GAY_RA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Giả thuyết giết người rồi tự sát", den: "Cặp đôi Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L4"], phu_dinh: false}},
  {tu: "Vết thương cũ trên tay Danie", den: "Lưỡi dao", props: {trang_thai: "suy_doan", nguon_chunk: ["25_phap-y#L4"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:GAY_RA_BOI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Tấm ảnh ngoại phạm của Johny", den: "Trận bóng", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L2"], phu_dinh: false}},
  {tu: "Bộ ảnh chụp ngôi nhà nạn nhân", den: "Ngôi nhà nạn nhân", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L3"], phu_dinh: false}},
  {tu: "Tấm ảnh chiếc xe", den: "Chiếc xe đậu trước ngôi nhà nạn nhân", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L4"], phu_dinh: false}},
  {tu: "Tấm ảnh ghi lời nói của Chris và Danie", den: "Chris", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L5"], phu_dinh: false}},
  {tu: "Tấm ảnh ghi lời nói của Chris và Danie", den: "Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L5"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:GHI_LAI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Vụ tự sát của ba mẹ Johny", den: "Vụ án Chris–Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L3"], phu_dinh: false, mo_ta: "Hiện trường vụ tự sát của ba mẹ Johny tương tự hiện trường vụ án Chris–Danie."}},
  {tu: "Vụ án Chris–Danie", den: "Vụ tự sát của ba mẹ Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L3"], phu_dinh: false, mo_ta: "Hiện trường vụ tự sát của ba mẹ Johny tương tự hiện trường vụ án Chris–Danie."}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:GIONG_VOI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Cảnh sát", den: "Chris giết Danie rồi sau đó Chris tự sát", props: {trang_thai: "loi_khai", nguon_chunk: ["10_chung_canh-sat#L1"], phu_dinh: false}},
  {tu: "Cảnh sát", den: "Cảnh sát không có thêm thông tin nào khác về vụ án", props: {trang_thai: "xac_nhan", nguon_chunk: ["10_chung_canh-sat#L2"], phu_dinh: false}},
  {tu: "Người dân khu phố", den: "Thường xuyên có tiếng la hét và đập phá từ ngôi nhà nạn nhân", props: {trang_thai: "tin_don", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L2"], phu_dinh: false, tan_suat: "thường xuyên, không chỉ riêng đêm án mạng"}},
  {tu: "Người dân khu phố", den: "Chris và Danie là những người kỳ dị hay khóa mình trong nhà", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], phu_dinh: false}},
  {tu: "Người dân khu phố", den: "Chris và Danie khóa mình trong nhà vì cãi nhau", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Người dân khu phố", den: "Chris từng đánh Danie", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Wills", den: "Có hai tiếng súng cách nhau 15 phút trong đêm án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false}},
  {tu: "Wills", den: "Chris là một gã bạn trai tồi tệ và bạo lực", props: {trang_thai: "suy_doan", nguon_chunk: ["20_wills#L2"], phu_dinh: false}},
  {tu: "Brad", den: "Chris là người có tham vọng và lạc quan", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L3"], phu_dinh: false}},
  {tu: "Brad", den: "Chris không dễ bị mất kiểm soát", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L3"], phu_dinh: false}},
  {tu: "Brad", den: "Chris và Danie đang yêu nhau sâu đậm", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L4"], phu_dinh: false}},
  {tu: "Bread", den: "Chris có điều bất thường trong những lần cuối đến tiệm bánh mì", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"], phu_dinh: false}},
  {tu: "Bread", den: "Có thể Chris đang có vấn đề với Danie", props: {trang_thai: "suy_doan", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"], phu_dinh: false}},
  {tu: "Bread", den: "4–5 tháng trước Chris khá bình tĩnh còn Danie đầy năng lượng và tích cực", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], phu_dinh: false}},
  {tu: "Johny", den: "Johny ở nhà khi vụ án xảy ra", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}},
  {tu: "Margaret", den: "Ba mẹ Johny thường xuyên cãi nhau và bạo lực khi Johny còn bé", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L2"], phu_dinh: false}},
  {tu: "Margaret", den: "Vụ tự sát của ba mẹ Johny tương tự hiện trường vụ án Chris–Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L3"], phu_dinh: false}},
  {tu: "Edward", den: "Danie bị bắn ở khoảng cách gần dưới 10cm", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L1"], phu_dinh: false}},
  {tu: "Edward", den: "Trên tay Danie không có dấu ẩu đả và không có dấu hiệu cầm súng", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L3"], phu_dinh: false}},
  {tu: "Edward", den: "Dấu vân tay có thể tiết lộ thêm thông tin", props: {trang_thai: "suy_doan", nguon_chunk: ["25_phap-y#L3"], phu_dinh: false}},
  {tu: "Edward", den: "Có thể ai đó hoặc chính Danie đã dùng lưỡi dao gây tổn thương cho Danie nhiều lần", props: {trang_thai: "suy_doan", nguon_chunk: ["25_phap-y#L4"], phu_dinh: false}},
  {tu: "Edward", den: "Vết vỡ thứ hai trên hộp sọ Chris là vết đạn bay ra", props: {trang_thai: "suy_doan", nguon_chunk: ["25_phap-y#L7"], phu_dinh: false}},
  {tu: "Edward", den: "Không chắc chắn Chris là người bắn Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L10"], phu_dinh: false}},
  {tu: "Jack", den: "Tối hôm trước án mạng Chris và Danie vẫn hòa thuận", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L4"], phu_dinh: false}},
  {tu: "Jack", den: "Chris và Danie không tự sát", props: {trang_thai: "suy_doan", nguon_chunk: ["26_jack#L5"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:KHAI_RANG]->(b)
SET r += row.props;

UNWIND [
  {tu: "Bread", den: "Tiệm bánh mì của Bread", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Jack", den: "Tiệm sửa xe của Jack", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L2"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:LAM_CHU]->(b)
SET r += row.props;

UNWIND [
  {tu: "Tấm ảnh ngoại phạm của Johny", den: "Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:LA_BANG_CHUNG_NGOAI_PHAM_CHO]->(b)
SET r += row.props;

UNWIND [
  {tu: "Ba mẹ Johny", den: "Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L2"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:LA_CHA_ME_CUA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris", den: "Tiệm bánh mì của Bread", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false, tan_suat: "mỗi ngày"}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:LA_KHACH_CUA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris", den: "Vụ án Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}},
  {tu: "Danie", den: "Vụ án Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}},
  {tu: "Cặp đôi Chris–Danie", den: "Vụ án Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}},
  {tu: "Danie", den: "Danie bị bắn", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L2"], phu_dinh: false}},
  {tu: "Chris", den: "Chris bị bắn vào đầu", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L6"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:LA_NAN_NHAN_TRONG]->(b)
SET r += row.props;

UNWIND [
  {tu: "Margaret", den: "Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L1"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:LA_ONG_BA_CUA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Tấm ảnh chiếc xe", den: "Giả thuyết có người thứ ba tại hiện trường", props: {trang_thai: "suy_doan", nguon_chunk: ["23_johny#L4"], phu_dinh: false}},
  {tu: "Vết cháy và tóc cháy trên thi thể Danie", den: "Danie bị bắn", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L1"], phu_dinh: false}},
  {tu: "Dấu vân tay", den: "Vụ án Chris–Danie", props: {trang_thai: "suy_doan", nguon_chunk: ["25_phap-y#L3"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:MANH_MOI_CHO]->(b)
SET r += row.props;

UNWIND [
  {tu: "Có hai tiếng súng cách nhau 15 phút trong đêm án mạng", den: "Giả thuyết giết người rồi tự sát", props: {trang_thai: "suy_doan", nguon_chunk: ["20_wills#L1"], phu_dinh: false, mo_ta: "Một người tự sát sau khi bắn người kia thì khó có khoảng cách 15 phút giữa hai phát."}},
  {tu: "Chris không dễ bị mất kiểm soát", den: "Giả thuyết Chris mất kiểm soát", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L3"], phu_dinh: false}},
  {tu: "Chris và Danie đang yêu nhau sâu đậm", den: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {trang_thai: "suy_doan", nguon_chunk: ["21_brad#L4"], phu_dinh: false}},
  {tu: "4–5 tháng trước Chris khá bình tĩnh còn Danie đầy năng lượng và tích cực", den: "Giả thuyết Chris mất kiểm soát", props: {trang_thai: "suy_doan", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L5"], phu_dinh: false}},
  {tu: "Trên tay Danie không có dấu ẩu đả và không có dấu hiệu cầm súng", den: "Giả thuyết Danie tự sát hoặc chống cự", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L3"], phu_dinh: false}},
  {tu: "Không chắc chắn Chris là người bắn Danie", den: "Giả thuyết giết người rồi tự sát", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L10"], phu_dinh: false}},
  {tu: "Tối hôm trước án mạng Chris và Danie vẫn hòa thuận", den: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L4"], phu_dinh: false}},
  {tu: "Chris và Danie không tự sát", den: "Giả thuyết giết người rồi tự sát", props: {trang_thai: "suy_doan", nguon_chunk: ["26_jack#L5"], phu_dinh: false}},
  {tu: "Chris và Danie không tự sát", den: "Giả thuyết Chris tự bắn mình", props: {trang_thai: "suy_doan", nguon_chunk: ["26_jack#L5"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:MAU_THUAN]->(b)
SET r += row.props;

UNWIND [
  {tu: "Phòng ăn", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Phòng khách", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Nhà bếp", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Mốc 15 phút sau tiếng súng thứ nhất", den: "Đêm xảy ra án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false}},
  {tu: "Nhà của Bread", den: "Khu phố", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L1"], phu_dinh: false}},
  {tu: "Tiệm bánh mì của Bread", den: "Khu phố", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Ngôi nhà nạn nhân", den: "Khu phố", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Đồng hồ trong tấm ảnh ngoại phạm", den: "Tấm ảnh ngoại phạm của Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}},
  {tu: "Thời điểm 2 phút trước vụ án", den: "Đêm xảy ra án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}},
  {tu: "Phòng Johny", den: "Nhà Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L6"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:NAM_TRONG]->(b)
SET r += row.props;

UNWIND [
  {tu: "Brad", den: "Chris", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L1"], phu_dinh: false, muc_do: "bạn từ năm cấp 3"}},
  {tu: "Chris", den: "Brad", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L1"], phu_dinh: false, muc_do: "bạn từ năm cấp 3"}},
  {tu: "Brad", den: "Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L2"], phu_dinh: false, muc_do: "gián tiếp, chỉ qua Chris và lời kể của Chris"}},
  {tu: "Danie", den: "Brad", props: {trang_thai: "loi_khai", nguon_chunk: ["21_brad#L2"], phu_dinh: false, muc_do: "gián tiếp, chỉ qua Chris và lời kể của Chris"}},
  {tu: "Jack", den: "Chris", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L1"], phu_dinh: false, muc_do: "bạn thân"}},
  {tu: "Chris", den: "Jack", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L1"], phu_dinh: false, muc_do: "bạn thân"}},
  {tu: "Jack", den: "Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L1"], phu_dinh: false, muc_do: "bạn thân"}},
  {tu: "Danie", den: "Jack", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L1"], phu_dinh: false, muc_do: "bạn thân"}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:QUEN_BIET]->(b)
SET r += row.props;

UNWIND [
  {tu: "Cặp đôi Chris–Danie", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Bread", den: "Nhà của Bread", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L1"], phu_dinh: false, thoi_diem: "qua nhiều thế hệ"}},
  {tu: "Chris", den: "Khu phố", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Margaret", den: "Nhà Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L1"], phu_dinh: false}},
  {tu: "Johny", den: "Nhà Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L1"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:SONG_TAI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Johny", den: "Tấm ảnh ngoại phạm của Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}},
  {tu: "Johny", den: "Bộ ảnh chụp ngôi nhà nạn nhân", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L3"], phu_dinh: false}},
  {tu: "Johny", den: "Tấm ảnh chiếc xe", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L4"], phu_dinh: false}},
  {tu: "Johny", den: "Tấm ảnh ghi lời nói của Chris và Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L5"], phu_dinh: false}},
  {tu: "Johny", den: "Đôi găng tay", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L6"], phu_dinh: false}},
  {tu: "Cặp đôi Chris–Danie", den: "Xe của cặp đôi Chris–Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L3"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:SO_HUU]->(b)
SET r += row.props;

UNWIND [
  {tu: "Jack", den: "Xe của cặp đôi Chris–Danie", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L3"], phu_dinh: false, muc_dich: "thăm hỏi Chris và Danie và sửa chữa chiếc xe"}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:SUA_CHUA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris", den: "Cặp đôi Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}},
  {tu: "Danie", den: "Cặp đôi Chris–Danie", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L1"], phu_dinh: false}},
  {tu: "James", den: "Cảnh sát", props: {trang_thai: "xac_nhan", nguon_chunk: ["10_chung_canh-sat#L1"], phu_dinh: false}},
  {tu: "Michael", den: "Cảnh sát", props: {trang_thai: "xac_nhan", nguon_chunk: ["10_chung_canh-sat#L1"], phu_dinh: false}},
  {tu: "Wills", den: "Người dân khu phố", props: {trang_thai: "xac_nhan", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false}},
  {tu: "Daniel", den: "Người dân khu phố", props: {trang_thai: "xac_nhan", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false}},
  {tu: "Thomas", den: "Người dân khu phố", props: {trang_thai: "xac_nhan", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false}},
  {tu: "Arthur", den: "Người dân khu phố", props: {trang_thai: "xac_nhan", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], phu_dinh: false}},
  {tu: "Clara", den: "Người dân khu phố", props: {trang_thai: "xac_nhan", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L1"], phu_dinh: false}},
  {tu: "Margaret", den: "Gia đình Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L1"], phu_dinh: false}},
  {tu: "Johny", den: "Gia đình Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L1"], phu_dinh: false}},
  {tu: "Ba mẹ Johny", den: "Gia đình Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L2"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:THANH_VIEN_CUA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Chris giết Danie rồi sau đó Chris tự sát", den: "Giả thuyết giết người rồi tự sát", props: {trang_thai: "suy_doan", nguon_chunk: ["10_chung_canh-sat#L1"], phu_dinh: false}},
  {tu: "Thường xuyên có tiếng la hét và đập phá từ ngôi nhà nạn nhân", den: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {trang_thai: "tin_don", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L2"], phu_dinh: false}},
  {tu: "Chris và Danie khóa mình trong nhà vì cãi nhau", den: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Chris từng đánh Danie", den: "Giả thuyết Chris mất kiểm soát", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Chris là một gã bạn trai tồi tệ và bạo lực", den: "Giả thuyết Chris mất kiểm soát", props: {trang_thai: "suy_doan", nguon_chunk: ["20_wills#L2"], phu_dinh: false}},
  {tu: "Có thể Chris đang có vấn đề với Danie", den: "Giả thuyết cặp đôi Chris–Danie bất hòa", props: {trang_thai: "suy_doan", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L4"], phu_dinh: false}},
  {tu: "Bộ ảnh chụp ngôi nhà nạn nhân", den: "Giả thuyết Johny theo dõi cặp đôi Chris–Danie", props: {trang_thai: "suy_doan", nguon_chunk: ["23_johny#L3"], phu_dinh: false}},
  {tu: "Tấm ảnh ghi lời nói của Chris và Danie", den: "Giả thuyết Johny theo dõi cặp đôi Chris–Danie", props: {trang_thai: "suy_doan", nguon_chunk: ["23_johny#L5"], phu_dinh: false}},
  {tu: "Vụ tự sát của ba mẹ Johny tương tự hiện trường vụ án Chris–Danie", den: "Giả thuyết có người thứ ba tại hiện trường", props: {trang_thai: "suy_doan", nguon_chunk: ["24_ba-johny#L3"], phu_dinh: false}},
  {tu: "Vết đạn trên trán Chris", den: "Giả thuyết Chris tự bắn mình", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L6"], phu_dinh: false}},
  {tu: "Dấu vết cầm súng trên tay trái Chris", den: "Giả thuyết Chris tự bắn mình", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L8"], phu_dinh: false}},
  {tu: "Dấu vết cầm súng trên tay trái Chris", den: "Giả thuyết giết người rồi tự sát", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L8"], phu_dinh: false}},
  {tu: "Không chắc chắn Chris là người bắn Danie", den: "Giả thuyết có người thứ ba tại hiện trường", props: {trang_thai: "suy_doan", nguon_chunk: ["25_phap-y#L10"], phu_dinh: false}},
  {tu: "Chris và Danie không tự sát", den: "Giả thuyết có người thứ ba tại hiện trường", props: {trang_thai: "suy_doan", nguon_chunk: ["26_jack#L5"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:UNG_HO]->(b)
SET r += row.props;

UNWIND [
  {tu: "Đồng hồ trong tấm ảnh ngoại phạm", den: "Tấm ảnh ngoại phạm của Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["23_johny#L1"], phu_dinh: false}},
  {tu: "Trận bóng", den: "Tấm ảnh ngoại phạm của Johny", props: {trang_thai: "suy_doan", nguon_chunk: ["23_johny#L2"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:XAC_DINH_THOI_DIEM_CUA]->(b)
SET r += row.props;

UNWIND [
  {tu: "Vụ án Chris–Danie", den: "Ngôi nhà nạn nhân", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L2"], phu_dinh: false}},
  {tu: "Tiếng la hét", den: "Ngôi nhà nạn nhân", props: {trang_thai: "tin_don", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false}},
  {tu: "Tiếng đập phá", den: "Ngôi nhà nạn nhân", props: {trang_thai: "tin_don", nguon_chunk: ["11_chung_tin-don-tieng-la-het#L1"], phu_dinh: false}},
  {tu: "Cãi nhau giữa Chris và Danie", den: "Ngôi nhà nạn nhân", props: {trang_thai: "tin_don", nguon_chunk: ["12_chung_tin-don-khoa-minh-trong-nha#L2"], phu_dinh: false}},
  {tu: "Chris chuyển tới khu phố", den: "Khu phố", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Chris ngừng đến tiệm bánh mì", den: "Tiệm bánh mì của Bread", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Chuyến thăm của Jack tối hôm trước án mạng", den: "Ngôi nhà nạn nhân", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L3"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:XAY_RA_TAI]->(b)
SET r += row.props;

UNWIND [
  {tu: "Vụ án Chris–Danie", den: "Đêm xảy ra án mạng", props: {trang_thai: "xac_nhan", nguon_chunk: ["00_boi-canh-chung#L3"], phu_dinh: false}},
  {tu: "Tiếng súng thứ nhất", den: "Đêm xảy ra án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false}},
  {tu: "Tiếng súng thứ hai", den: "Đêm xảy ra án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["20_wills#L1"], phu_dinh: false}},
  {tu: "Chris ngừng đến tiệm bánh mì", den: "Dạo gần đây", props: {trang_thai: "loi_khai", nguon_chunk: ["22_ong-lao-tiem-banh-mi#L3"], phu_dinh: false}},
  {tu: "Xung đột bạo lực của ba mẹ Johny", den: "Thời thơ ấu của Johny", props: {trang_thai: "loi_khai", nguon_chunk: ["24_ba-johny#L2"], phu_dinh: false}},
  {tu: "Vết thương cũ trên tay Danie", den: "Nhiều năm trước", props: {trang_thai: "xac_nhan", nguon_chunk: ["25_phap-y#L4"], phu_dinh: false}},
  {tu: "Chuyến thăm của Jack tối hôm trước án mạng", den: "Tối hôm trước án mạng", props: {trang_thai: "loi_khai", nguon_chunk: ["26_jack#L3"], phu_dinh: false}}
] AS row
MATCH (a:ThucThe {ten: row.tu})
MATCH (b:ThucThe {ten: row.den})
MERGE (a)-[r:XAY_RA_VAO]->(b)
SET r += row.props;
