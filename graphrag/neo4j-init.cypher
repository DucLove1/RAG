// =====================================================================================
// graphrag/neo4j-init.cypher  -  schema cua do thi tri thuc vu an Chris-Danie.
//
// Chay MOT LAN truoc lan nap dau tien. Neo4j Browser chi chay duoc MOT cau moi lan
// (bo dau ; roi chay lan luot); cypher-shell chay ca file:
//     cypher-shell -a <uri> -u <user> -p <pass> -f neo4j-init.cypher
//
// Constraint UNIQUE phai co TRUOC lan nap dau: MERGE khong constraint co the de ra ban
// trung, va Neo4j tu choi them constraint sau khi da co trung.
// =====================================================================================

// Khoa dinh danh cua moi thuc the. Ten la khoa, khong phai id sinh tu dong.
CREATE CONSTRAINT thucthe_ten_unique IF NOT EXISTS
FOR (e:ThucThe) REQUIRE e.ten IS UNIQUE;

// Truy van phuc vu nguoi choi luon bat dau bang MATCH (npc:NhanVat {ten: $npc}).
CREATE INDEX nhanvat_ten IF NOT EXISTS
FOR (n:NhanVat) ON (n.ten);

// Index fulltext: duong tra loi KHONG con dung (hat giong gio la thuc the do LLM chon trong
// danh muc NPC duoc biet). Giu lai vi re va tien khi tra tay trong Neo4j Browser.
CREATE FULLTEXT INDEX thucthe_fulltext IF NOT EXISTS
FOR (e:ThucThe) ON EACH [e.ten];

// Bi danh: "cap doi", "hien truong", va vai tro cu cua tung NPC ("Canh sat 1" -> James).
CREATE FULLTEXT INDEX thucthe_bidanh IF NOT EXISTS
FOR (e:ThucThe) ON EACH [e.bi_danh];
