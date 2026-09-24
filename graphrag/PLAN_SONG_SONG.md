# GraphRAG: Qdrant và Neo4j chạy song song, LLM trích thực thể

## Context

**Flow hiện tại** chạy nối tiếp:
```
embedding → cache → Qdrant top-K → lấy mã chunk của hit làm hạt giống → Neo4j mở rộng → prompt
```
Neo4j phải chờ Qdrant trả về xong mới chạy được. Hạt giống lại phụ thuộc vào việc vector có khớp đúng dòng hay không. Khi Qdrant trượt thì đồ thị cũng trượt theo, và chỉ còn fulltext theo chữ làm dự phòng.

**Flow mới** mà user yêu cầu: hai nhánh độc lập, chạy song song, gộp kết quả ở cuối.
```
                       embedding → tra cache ──trúng──► trả lời
                                      │ trượt
               ┌──────────────────────┴───────────────────────┐
     [A] Qdrant KNN (vector đã có)             [B] LLM trích thực thể + ý định quan hệ
               │                                    (chỉ chọn trong danh mục thực thể của KG
               │                                     mà NPC được biết, và loại quan hệ trong ontology)
               │                                              │
               │                                  Neo4j mở rộng quanh các thực thể đó
               │                                              │
               │                                  tra nguyên văn các dòng mà cạnh trỏ tới
               └──────────────────────┬───────────────────────┘
                              gộp + chia ngân sách → prompt → LLM trả lời
```

**Quyết định đã chốt với user:**
1. **Bỏ hẳn** cách lấy hạt giống từ mã chunk vector. Chỉ còn flow mới, và KLTN so hai cấu hình: RAG thuần (`Graph:Enabled=false`) và GraphRAG trích thực thể.
2. Thực thể mà LLM được chọn **lấy từ chính knowledge graph**: danh mục tên và bí danh các node mà NPC được biết. LLM không được tự đặt tên mới.
3. Hai nhánh tách ra **sau** tra cache. Trúng cache thì không tốn lượt LLM trích nào.
4. Loại quan hệ mà LLM trích ra dùng để **ưu tiên** (xếp lên đầu), không dùng để lọc cứng.

**Giữ nguyên:**
- Thứ tự `AskPipeline` / `AskStreamPipeline`: chuẩn hóa → route → điểm yếu → embedding → cache → `IAskContextBuilder` → LLM.
- `IAskContextBuilder` vẫn là điểm gộp duy nhất, nên hai pipeline **không phải sửa**.
- Mọi bất biến ACL của Cypher: lọc quyền 3 lần, với cạnh lập luận dùng `all`.
- Khử trùng cạnh đối xứng, lọc `trang_thai`, sắp toàn phần.
- Prompt giống hệt bản cũ từng byte khi đồ thị rỗng hoặc bị tắt.
- Cờ `Degraded` chặn ghi cache.

---

## 0. Chép plan vào repo (bước đầu tiên, trước khi sửa code)

Chép nguyên nội dung file plan này sang **`graphrag/PLAN_SONG_SONG.md`**, cạnh `SCHEMA.md`.

## 1. Danh mục thực thể theo NPC (nguồn là Neo4j)

**`IGraphStore`** thêm một method:
```csharp
Task<GraphStoreResult<GraphCatalogEntry>> ListKnownEntitiesAsync(string npcName, CancellationToken ct = default);
record GraphCatalogEntry(string Name, IReadOnlyList<string> Aliases, IReadOnlyList<string> Labels);
```
- Cypher mới `GraphCypher.KnownEntitiesTemplate`:
  - Tìm NPC theo `ten`/`bi_danh`.
  - Lấy mọi node `e` có `any(m IN e.nguon_chunk WHERE split(m,'#')[0] IN npc.duoc_biet)`.
  - Trả về `ten`, `bi_danh` và `labels(e)` trừ nhãn nền, sắp theo `ten`.
- Lấy từ Neo4j chứ không đọc `entities.json`. `duoc_biet` là nơi duy nhất giữ ACL, và file `entities.json` chỉ nạp ở nhánh admin.

**`IGraphEntityCatalog` → `CachedGraphEntityCatalog`** (`Class/Graph/`):
- Bọc `IGraphStore.ListKnownEntitiesAsync` bằng `IMemoryCache`, key theo NPC, TTL `Graph:Extraction:CatalogCacheMinutes`.
- Chỉ cache kết quả thành công. Neo4j lỗi thì trả `Failed` và không cache.
- Phơi ra `Resolve(string mention)`: nhận tên hoặc bí danh, so không phân biệt hoa thường sau khi `Trim`, trả về `ten` chuẩn hoặc `null`.

## 2. LLM trích thực thể + ý định quan hệ

**`Interface/IGraphEntityExtractor.cs`**
```csharp
record GraphExtraction(IReadOnlyList<string> Entities, IReadOnlyList<string> RelationTypes, bool Failed)
{ static Empty; static FailedResult; }
Task<GraphExtraction> ExtractAsync(string npcName, string question, CancellationToken ct = default);
```

**`Class/Graph/LlmGraphEntityExtractor.cs`** theo đúng khuôn của `LlmWeakPointDetector` và `LlmQueryNormalizer`:
- **Provider:** `ILlmProviderResolver.Resolve(config.Provider)` (keyed), `AskAsync(system, user, config.Model, ct)`.
- **System prompt:** `string.Format(SystemPromptTemplate, npc, entityCatalog, relationCatalog, MaxEntities, MaxRelationTypes)`.
  - `entityCatalog`: mỗi dòng một thực thể theo `EntityLineTemplate`, ví dụ `- Khẩu súng (VatChung; bí danh: súng)`.
  - `relationCatalog`: mỗi dòng `- CO_DAU_VET_CAM: có dấu vết cầm`, dựng từ `IOntology.Relations` + `DisplayRelation`, tính một lần trong constructor.
  - Dặn LLM: câu hỏi xưng "anh/cậu/ông/bạn" với NPC thì thêm **tên NPC** vào danh sách thực thể.
- **Đầu ra JSON** `{"thuc_the": [...], "quan_he": [...]}`. Tên trường nằm trong `Class/Constants/GraphExtractionFields.cs`.
  - Parser: bỏ code fence, cắt từ `{` đầu tiên tới `}` cuối cùng, rồi `System.Text.Json`. Hiện codebase chưa có JSON mode.
- **Hậu kiểm, không tin LLM:**
  - Mỗi tên đi qua `catalog.Resolve`. Tên không có trong danh mục thì **bỏ**.
  - Loại quan hệ phải ∈ `IOntology.Relations`.
  - Khử trùng, giữ thứ tự LLM trả về (dùng làm thứ hạng hạt giống), cắt còn `MaxEntities` / `MaxRelationTypes`.
- **Chịu lỗi:**
  - `MaxInputLength` chặn câu quá dài và trả `Empty`.
  - Timeout riêng: `.WaitAsync(TimeoutMs)`. Helper khác dựa vào timeout 30s của HttpClient Gemini, nhưng nhánh này nằm trên đường nóng.
  - Exception, timeout, JSON hỏng hoặc danh mục `Failed` → `FailedResult`. `OperationCanceledException` của caller vẫn ném tiếp.
  - JSON hợp lệ nhưng rỗng → `Empty`, không tính là suy biến.

**`Class/Graph/CachingGraphEntityExtractor.cs`** (decorator):
- `IMemoryCache` theo `(npc, câu hỏi đã chuẩn hóa)`, TTL `ResultCacheMinutes`, chỉ cache kết quả không `Failed`.
- Lý do: LLM chạy temperature 0.5 (chung cả provider Gemini). Không có cache thì cùng một câu hỏi có thể ra hạt giống khác nhau, và phép đo KLTN không lặp lại được.

**Config** `GraphExtractionConfig`, section `Graph:Extraction`, đặt trong [GraphConfig.cs](RAG/Class/Config/GraphConfig.cs):

| Khóa | Giá trị |
|---|---|
| `Provider` | `Gemini` |
| `Model` | `gemini-3.1-flash-lite` |
| `MaxInputLength` | 500 |
| `MaxEntities` | 5 |
| `MaxRelationTypes` | 4 |
| `TimeoutMs` | 4000 |
| `CatalogCacheMinutes` | 30 |
| `ResultCacheMinutes` | 60 |
| Template | `SystemPromptTemplate`, `UserPromptTemplate`, `EntityLineTemplate`, `EntityAliasTemplate`, `RelationLineTemplate`, `CatalogSeparator` |

Tất cả nằm trong `appsettings.json`, không hardcode.

## 3. Neo4j mở rộng theo tên thực thể

**`GraphCypher.ExpandTemplate` viết lại** thành bản seed theo tên. Bản seed theo mã chunk bị xóa.
```cypher
MATCH (npc:{0}) WHERE npc.ten = $npc OR $npc IN coalesce(npc.bi_danh, [])
UNWIND range(0, size($thucThe) - 1) AS hang
MATCH (e:{1} {ten: $thucThe[hang]})
WHERE any(m IN e.nguon_chunk WHERE split(m,'#')[0] IN npc.duoc_biet)
MATCH (e)-[r]-(lc:{1})
WHERE <giữ nguyên: trang_thai, ACL lc, ACL r (all/any), khử đối xứng, chiLapLuan>
WITH r, min(hang) AS hang,
     max(CASE WHEN lc.ten IN $thucThe THEN 1 ELSE 0 END) AS noi_hai   -- cạnh nối 2 thực thể được hỏi
WITH r, hang, ..., CASE WHEN type(r) IN $loaiQuanTam THEN 0 ELSE 1 END AS khop_y_dinh
ORDER BY hang, noi_hai DESC, khop_y_dinh, uu_tien, lap_luan, nguon, quan_he, dich
<giữ nguyên: collect theo hang → UNWIND vong → ORDER BY vong, hang → LIMIT>
```
- `hang` là thứ tự thực thể do LLM trả về, thay cho thứ hạng vector. Cơ chế lấy xen kẽ giữ nguyên.
- **Bỏ `e <> npc`**. NPC giờ chỉ thành hạt giống khi câu hỏi nói về chính họ. Trước đây node NPC lọt vào qua mọi dòng lời khai, còn bây giờ việc chọn do ý định câu hỏi quyết định, và `khop_y_dinh` giữ các cạnh liên quan ở trên đầu.
- Tham số mới `GraphCypher.Parameters`: `Entities = "thucThe"`, `IntentTypes = "loaiQuanTam"`. Cột `truc_tiep` bị bỏ.
- `GraphExpansionRequest(NpcName, EntityNames, IntentRelationTypes, ReasoningOnly, Limit)` thay cho bản dùng `ChunkCodes`.
- `Neo4jGraphStore.WarmUpAsync` chạy template mới với tên `__warmup__`, và thêm một lượt `ListKnownEntitiesAsync` để nạp sẵn kế hoạch truy vấn của câu Cypher đó.

**Xóa** đường fulltext, vì danh mục trong prompt đã thay vai trò của nó:
- `FindSeedsByTextAsync`, `FulltextSeedTemplate`, `BuildFulltextQuery`, `GraphSeed`.
- `FulltextFallbackEnabled` và `FulltextMinRelativeScore`, cả trong code lẫn trong appsettings.
- Index fulltext trong `IGraphSchemaAdmin` **giữ lại**. Nó rẻ và vẫn dùng được khi tra tay trong Neo4j Browser.

## 4. `IGraphSearch` mới: `EntityGraphSearch` thay `LocalGraphSearch`

- `GraphSearchQuery(NpcName, Question, RelationBudget)`: bỏ `SeedChunkCodes`. `IGraphSearch` vẫn là ranh giới, giữ chỗ cắm global search sau này.
- `Class/Graph/EntityGraphSearch.cs`:
  - Trích thực thể. `Failed` → `DegradedEmpty`; rỗng → `Empty`, không gọi Neo4j.
  - Gọi `ExpandAsync(request mới)`. `Failed` → `DegradedEmpty`.
  - Phần khử trùng theo `ElementId`, gom mã và `ChunkCodes.Neighbours` **chép nguyên** từ `LocalGraphSearch.SearchAsync`.
  - Trả thêm `Extraction` trong `GraphContext` để debug và log: `GraphContext(Relations, SupportingChunkCodes, Degraded, Extraction?)`.
- Xóa `LocalGraphSearch.cs`. `NullGraphSearch` giữ nguyên.

## 5. `AskContextBuilder`: hai nhánh song song, gộp ở cuối

Sửa [AskContextBuilder.cs](RAG/Class/Answering/AskContextBuilder.cs):
```csharp
using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
var vectorTask = _vectorStore.SearchAsync(questionEmbedding, filter, topK, ct);
var graphTask  = SearchGraphBranchAsync(npcName, question, linked.Token);   // search đồ thị → tra nguyên văn
try { await Task.WhenAll(vectorTask, graphTask); }
catch { linked.Cancel(); throw; }                                           // Qdrant lỗi: hủy nhánh đồ thị, ném như cũ
```
- `SearchGraphBranchAsync` = `_graphSearch.SearchAsync`, rồi `_chunkTextLookup.GetByCodesAsync(npc, SupportingChunkCodes)`. Bước tra nguyên văn nằm **trong** nhánh B, nên nó cũng chạy song song với Qdrant.
  - Tra nguyên văn lỗi → coi là suy biến, không ném, vì nhánh đồ thị không bao giờ được làm hỏng request.
- **Gộp:**
  - Hit vector vào nguyên vẹn, nối y như cũ.
  - Nối thêm nguyên văn từ nhánh đồ thị theo thứ tự cạnh, **bỏ các mã đã có trong hit vector** (so bằng `chunkCode` trong payload), dừng ở dòng đầu tiên vượt ngân sách.
  - Khối đồ thị render như cũ.
  - Logic `AppendSupportingTextAsync` đổi thành bản đồng bộ nhận dictionary đã tra sẵn.
- `AskContext` giữ nguyên hình dạng. `Cacheable` vẫn là `HasContext && !GraphDegraded`.
- `AskPipeline` / `AskStreamPipeline`: **không sửa**.

## 6. DI, quan sát, debug

- [GraphServiceCollectionExtensions.cs](RAG/Extension/DependencyInjection/GraphServiceCollectionExtensions.cs), nhánh `Enabled`:
  - `AddValidatedOptions<GraphExtractionConfig>`.
  - `IGraphEntityCatalog` → `CachedGraphEntityCatalog`.
  - `IGraphEntityExtractor` → `LlmGraphEntityExtractor`, bọc bởi `CachingGraphEntityExtractor` qua `Decorate<>` trong `ServiceCollectionDecorationExtensions`.
  - `IGraphSearch` → `EntityGraphSearch`.
  - Cần `AddMemoryCache()`. Kiểm xem đã có chưa (`MemoryQueryCache` có thể đã đăng ký).
- **Latency:**
  - `LatencyStages` thêm `graphSearch` (cả nhánh B), `graphExtract` và `chunkResolve`.
  - `LatencyTags` thêm `entities` và `relations`.
  - Thêm decorator `TimedGraphSearch`, `TimedGraphEntityExtractor`, `TimedChunkTextLookup` trong `DecorateRagStack`. Đăng ký chỉ khi đồ thị bật; với `NullGraphSearch` thì bọc vẫn vô hại.
  - Sửa comment lệch ở `RagStackServiceCollectionExtensions.cs` L29-31.
  - Log một request phải cho thấy `vectorSearch` và `graphSearch` chồng nhau về thời gian.
- **[GraphAdminController.cs](RAG/Controllers/GraphAdminController.cs):**
  - `local-search-debug`: `GraphDebugRequest(NpcName, Question, Limit)`, bỏ `ChunkCodes`. Response thêm `extraction: { entities, relationTypes, failed }`.
  - Endpoint mới `GET api/graph/catalog-debug?npcName=` trả danh mục thực thể của NPC, dùng để kiểm ACL bằng mắt.
  - `context-debug` giữ nguyên.
- Cập nhật tài liệu:
  - [RAG.http](RAG/RAG.http): G-4..G-6 đổi sang body có câu hỏi, thêm G-11 catalog-debug.
  - [graphrag/SCHEMA.md](graphrag/SCHEMA.md): bất biến 4, hạt giống theo tên, `noi_hai`/`khop_y_dinh`.
  - Doc comment trong `GraphCypher.cs`.

## File trọng yếu

- **Sửa:** `Interface/IGraphStore.cs`, `Interface/IGraphSearch.cs`, `Class/Constants/GraphCypher.cs`, `Class/Graph/Neo4jGraphStore.cs`, `Class/Answering/AskContextBuilder.cs`, `Class/Config/GraphConfig.cs`, `Class/Constants/LatencyStages.cs`, `LatencyTags.cs`, `Extension/DependencyInjection/GraphServiceCollectionExtensions.cs`, `LatencyServiceCollectionExtensions.cs`, `Controllers/GraphAdminController.cs`, `appsettings.json`, `RAG.http`, `graphrag/SCHEMA.md`.
- **Thêm:** `Interface/IGraphEntityExtractor.cs`, `Interface/IGraphEntityCatalog.cs`, `Class/Graph/LlmGraphEntityExtractor.cs`, `CachingGraphEntityExtractor.cs`, `CachedGraphEntityCatalog.cs`, `EntityGraphSearch.cs`, `Class/Constants/GraphExtractionFields.cs`, `Class/Diagnostics/Timing/TimedGraphSearch.cs`, `TimedGraphEntityExtractor.cs`, `TimedChunkTextLookup.cs`.
- **Xóa:** `Class/Graph/LocalGraphSearch.cs`.
- **Tái dùng:**
  - `ILlmProviderResolver` (`Class/KeyedLlmProviderResolver.cs`), theo khuôn `LlmWeakPointDetector`.
  - `IOntology.Relations` / `DisplayRelation` (`Class/Graph/JsonOntology.cs`).
  - `Neo4jGraphStore.RunAsync` (breaker + hai timeout).
  - `ChunkCodes.Neighbours`, `IChunkTextLookup`, `IGraphContextRenderer`.
  - `ServiceCollectionDecorationExtensions.Decorate<>`, `AddValidatedOptions<>`.

## Ngoài phạm vi (nhắc lại từ plan trước)

- **Phase 5a:** vân tay cách dựng ngữ cảnh trong `AnswerCachePartition`. Sau khi đổi flow thì **xóa cache câu trả lời một lần bằng tay**, nếu không các entry dựng theo flow cũ vẫn được phục vụ.
- **Phase 6:** chặn boot khi `Chunking:Strategy != Line`, map exception sang mã HTTP.

## Kiểm chứng

Build ra thư mục tạm (`dotnet build -o %TEMP%/ragbuild`) vì app của user đang giữ khóa `RAG.exe`. Chạy trên cổng phụ, gọi API bằng Python urllib, vì curl trong Git Bash làm hỏng UTF-8.

| # | Bài | Kỳ vọng |
|---|---|---|
| 1 | `catalog-debug` Edward và Jack | Danh mục của Jack **không** có thực thể chỉ xuất thân từ `25_phap-y`. Có tên bí danh. |
| 2 | `local-search-debug` Edward "tay của Chris có dấu vết gì không" | `entities` ⊇ `Chris`, `relationTypes` có `CO_DAU_VET_CAM`; cạnh `Chris … Khẩu súng` nằm trong top đầu |
| 3 | Cùng câu, NPC Jack | Không cạnh nào có `ma_chunk` bắt đầu bằng `25_phap-y` |
| 4 | Johny "trong phòng cậu người ta tìm thấy thứ gì" | `entities` có `Johny` (tự xưng), khối đồ thị có cạnh đôi găng tay |
| 5 | Câu nêu tên không có trong KG ("con mèo của Chris") | Tên lạ bị bỏ, không lỗi, không `degraded` |
| 6 | Đặt sai model hoặc `TimeoutMs=1` | `degraded: true`, `/ask` vẫn 200 bằng RAG thuần, cache **không** ghi |
| 7 | `context-debug` G-9/G-10 + log latency | `vectorSearch` và `graphSearch` chồng nhau; thời gian dựng ngữ cảnh ≈ max chứ không phải tổng hai nhánh |
| 8 | Chạy bài 2 năm lần | Quan hệ giống hệt cả nội dung lẫn thứ tự (nhờ cache kết quả trích) |
| 9 | `Graph:Enabled=false` rồi `context-debug` | Prompt giống từng byte với RAG thuần, không gọi LLM trích |
| 10 | `/api/query/ask` thật cho Edward | Câu trả lời dùng được tri thức trong khối đồ thị |
