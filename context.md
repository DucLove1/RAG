# Context bàn giao — RAG NPC cho game trinh thám

Tài liệu này để một phiên làm việc mới nắm được bối cảnh mà không phải đọc lại toàn bộ code.
Chi tiết đầy đủ kèm số liệu đo: [docs/semantic-router-plan.md](docs/semantic-router-plan.md).

---

## 1. Dự án là gì

Backend RAG cho **NPC trong một game trinh thám tiếng Việt** (tham chiếu: *Rainswept*).
Người chơi vào vai thám tử điều tra vụ án Chris/Danie; NPC gồm cảnh sát, dân địa phương
(Wills, Brad, ông lão bánh mì, Johny, bà Johny) và pháp y. Thiết kế game ở `RAG/Design game.docx`.

**Điểm quan trọng về persona:** NPC là *con người sống trong thế giới vụ án*, không phải trợ lý ảo.
Mọi prompt phải giữ nguyên tắc này.

### Stack

| Thành phần | Công nghệ |
|---|---|
| Web framework | ASP.NET Core **.NET 9**, C#, `Nullable=enable` |
| LLM trả lời | **Groq** (`openai/gpt-oss-120b`) qua SDK `OpenAI` |
| LLM chuẩn hóa | **Gemini** (`gemini-3.1-flash-lite`) qua HttpClient |
| Embedding | **Gemini** (`models/gemini-embedding-2`), 768 chiều |
| Vector store | **Qdrant Cloud** qua gRPC |
| Test | **Không có** (chưa có test project) |

Chỉ 4 NuGet package: `DotNetEnv`, `Microsoft.AspNetCore.OpenApi`, `OpenAI`, `Qdrant.Client`.

---

## 2. Quy ước code của dự án (BẮT BUỘC tuân theo)

Người dùng có yêu cầu rõ ràng, đã áp dụng nhất quán trong toàn bộ codebase:

1. **SOLID.**
2. **Không hardcode literal.** Mọi prompt, ngưỡng, template, ký tự nối đều nằm trong
   `appsettings.json` và bind vào options class có `public const string SectionName`.
   Prompt **không bao giờ** là string literal trong code — luôn qua `string.Format` với template từ config.
3. **Hằng số giao thức** (ràng buộc của nhà cung cấp, không phải cấu hình người dùng) đặt trong
   `Class/Constants/` kèm XML comment giải thích rõ sự khác biệt đó.
4. **XML doc comment viết bằng tiếng Việt**, giải thích *tại sao* chứ không mô tả lại code.
5. **Null Object thay vì cờ if.** Node bị tắt thì đăng ký bản passthrough, pipeline không cần biết
   đến `Enabled`. Đã dùng cho `IQueryNormalizer`, `ISemanticRouter`, `IRouteVectorCache`,
   `IRouteUtteranceStore`, `IQueryCache`.
8. **Decorator** để thêm hành vi ngang (cache) mà không sửa consumer — xem `Class/Caching/`.
6. **Keyed Services** cho nhiều implementation cùng interface (`ILLMProvider` → Groq/Gemini).
   `KeyedLlmProviderResolver` là **nơi duy nhất** trong application code chạm `IServiceProvider`
   (composition root trong `Extension.cs` thì không tính).
7. **Đăng ký DI theo module** ở `Extension/DependencyInjection/`, mỗi node một file
   `*ServiceCollectionExtensions.cs` với một method `AddXxx`. `AddRagStack` gọi hết theo đúng
   thứ tự — thứ tự đó là quan trọng (cache phải đăng ký trước các thành phần bị bọc) nên nó
   được khóa trong code chứ không nằm rải trong `Program.cs` như trước.

---

## 3. Luồng xử lý

`POST api/query/ask` → `RAGPipline.AskAsync`:

```
1. Chuẩn hóa câu hỏi        (IQueryNormalizer, Gemini flash-lite, fail-open)
2. Embedding MỘT LẦN         (dùng chung cho cả định tuyến lẫn truy hồi)
3. Định tuyến ngữ nghĩa      (ISemanticRouter, thuần in-memory, đồng bộ)
   ├─ khớp route  → AnswerWithoutRetrievalAsync  (BỎ QUA Qdrant hoàn toàn)
   └─ null        → AnswerWithRetrievalAsync     (EnsureCollection → Search → LLM)
```

Bước 1 và 2 đều đi qua **decorator cache** (`CachingQueryNormalizer`, `CachingEmbeddingProvider`),
nên câu lặp lại tốn 0 lần gọi Gemini: đo được 5.30s → 0.36s. Xem mục 4b.

**Lý do embedding trước rồi mới định tuyến:** nếu router tự nhúng bên trong thì mọi request đi
đường RAG (đa số) sẽ tốn **2 lần** gọi embedding. Vì vậy `ISemanticRouter.Route()` nhận sẵn `float[]`.

### Endpoints

| Method | Route | Chức năng |
|---|---|---|
| POST | `api/query/ask` | Hỏi NPC |
| POST | `api/query/upload` | Nạp tài liệu |
| POST | `api/query/create-collection` | Tạo collection Qdrant |
| POST | `api/query/route-debug` | **Chẩn đoán định tuyến** — trả điểm mọi route, không gọi LLM, không chạm Qdrant |
| POST | `api/query/route-utterances` | **Thêm câu mẫu lúc chạy** (text hoặc vector), hiệu lực ngay |
| GET | `api/query/cache-stats` | **Tỉ lệ trúng cache** hỏi đáp |
| GET | `api/query/check-health` | Health check |

`route-debug` là công cụ chính để tinh chỉnh ngưỡng — rẻ, lặp lại thoải mái.

---

## 4. Semantic Router (phần chính đã xây)

4 route "thoát sớm", **không có route `rag`** — RAG là đường mặc định khi không route nào khớp.

| Route | Ngưỡng | Câu mẫu | Vai trò |
|---|---|---|---|
| `chitchat` | 0.78 | 40 | Chào hỏi, tán gẫu |
| `farewell` | 0.78 | 39 | Tạm biệt |
| `thanks` | 0.78 | 40 | Cảm ơn, khen |
| `out_of_scope` | 0.78 | 39 | **Guardrail** — chặn câu ngoài thế giới game |

Cấu hình ở `appsettings.json` mục `SemanticRouter`. Thêm route mới = thêm một object JSON, không sửa code.

### Cơ chế

- **Điểm route = MAX cosine** trên các câu mẫu (không phải trung bình — cố định trong code, chủ ý
  không cấu hình hóa; xem lý do trong plan doc).
- `MaxRoutableLength = 60` — câu dài hơn thì bỏ qua chấm điểm, đi thẳng RAG.
- **Fail-open tuyệt đối.** Chưa warm xong, Gemini lỗi, config sai, vector lệch chiều → đều trả `null`
  → chạy RAG. Router không bao giờ được làm hỏng hệ thống.
- **`volatile` field + copy-on-write.** Đọc không cần khóa; ghi (warm-up hoặc thêm câu mẫu) dựng
  danh sách mới rồi gán một lần. `SemaphoreSlim` chỉ bọc đường ghi.
- **Warm-up chạy nền** (`SemanticRouterWarmupService`, `BackgroundService`), cố tình **không chặn
  startup** — Gemini sập không được phép làm container crash-loop.
- **Cache vector tăng dần** (`App_Data/route-vectors.json`). Vân tay = model + số chiều thôi; từng
  câu mẫu là khóa riêng → thêm/bớt câu chỉ nhúng phần chênh lệch. Sửa prompt hay ngưỡng **không**
  làm mất cache.
- **Câu mẫu thêm lúc chạy** lưu riêng ở `App_Data/route-utterances.json`, tự mang vector nên
  restart không phải nhúng lại. Cố tình không ghi ngược vào `appsettings.json`.
- **Đường dẫn tương đối giải theo `ContentRootPath`** (`RAG/Extension/AppDataPath.cs`), nên cả ba
  file cache nằm ở `<project>/App_Data/` — thấy được trong project và sống qua `dotnet clean`.

Khởi động: lần đầu ~2–3 phút (nhúng thật), các lần sau **~1 giây** từ cache.

---

## 4b. Cache đường hỏi đáp

Hai decorator bọc quanh bản thật; pipeline không biết có cache hay không:

- `CachingQueryNormalizer : IQueryNormalizer` — tiết kiệm 1 lần gọi LLM (~300–800ms)
- `CachingEmbeddingProvider : IEmbeddingProvider` — tiết kiệm 1 lượt trong hạn mức 100/phút

Cả hai dùng `IQueryCache` (abstraction riêng, không gọi thẳng `IMemoryCache`) → `MemoryQueryCache`
có chặn trần entry, hoặc `NullQueryCache` khi tắt. Cấu hình ở mục `QueryCache`.

Đo được: `"xin chào ông"` lần 1 **5.30s**, lần 2 **0.36s**. Phần còn lại là lần gọi Groq —
**cố ý không cache** vì NPC trả lời khác nhau mỗi lần là điều mong muốn.

**Cộng hưởng với node chuẩn hóa:** `"xin chao ong"` chưa từng gặp vẫn trúng cache embedding, vì
chuẩn hóa gom nó về cùng `"Xin chào ông."`. Một entry embedding phục vụ cả chùm biến thể sai chính tả.

**Sống qua restart.** Cache được ghi ra `App_Data/query-cache.bin` (nhị phân, ~3KB/vector) theo kiểu
write-behind: flush định kỳ `FlushIntervalSeconds` + flush lúc tắt. Đo được sau restart: nạp 6 entry
từ đĩa, câu cũ trúng cache ngay ở request đầu tiên.

`GET api/query/cache-stats` để xem tỉ lệ trúng.

---

## 5. CẠM BẪY — đọc kỹ trước khi sửa

### 5.1 Hạn mức Gemini: batch KHÔNG tiết kiệm quota

`batchEmbedContents` có hoạt động, nhưng Google tính **mỗi câu trong lô là một request**.
Free tier: `embed_content_free_tier_requests` = **100/phút**.

→ `Gemini:BatchSize = 50`, `Gemini:BatchDelaySeconds = 60`. Batch chỉ tiết kiệm **thời gian**.

### 5.2 TUYỆT ĐỐI không lùi về nhúng từng câu khi gặp 429

Đây là lỗi đã mắc và đã sửa. Lùi về one-by-one khi bị rate limit = nện thêm hàng trăm request vào
API đang từ chối. `EmbeddingRateLimitedException` tách riêng ca này: `404/400`/lỗi mạng thì lùi,
`429` thì **dừng ngay** và để warm-up thử lại sau.

### 5.3 `GetEmbeddingsAsync` nuốt lỗi HTTP — ĐÃ SỬA

**Trước đây:** `//response.EnsureSuccessStatusCode();` bị comment, nên lỗi API trả
`Array.Empty<float>()` chứ không ném. Vector rỗng đi thẳng vào truy hồi Qdrant, cho ra ngữ cảnh rác,
rồi LLM dựng câu trả lời trên đống rác đó — sai hoàn toàn mà không có triệu chứng nào.

**Bây giờ:** `PostSingleAsync` ném cho MỌI trường hợp không lấy được vector dùng được — kể cả
phản hồi 200 nhưng rỗng. `EmbeddingRateLimitedException` (429) → **429**,
`EmbeddingUnavailableException` (còn lại) → **503**, ánh xạ trong `RagExceptionHandler`.

**Vẫn giữ nguyên:** đường *batch* tiếp tục trả `null` cho lỗi lùi-được (404/400/lỗi mạng) để lùi về
nhúng từng câu — chỉ đường *single* mới ném. Và mọi vector vẫn phải qua `IsUsable()` trước khi vào
cache: xem 5.3b, lý do đó độc lập với việc ném hay không.

### 5.3b Cache kết quả fail-open sẽ đóng băng lỗi tạm thời thành VĨNH VIỄN

Hệ quả trực tiếp của 5.3, và là chỗ dễ sai nhất khi ai đó sửa lớp cache:

- **Embedding**: chỉ được cache vector đúng số chiều và khác vector 0. Cache một vector rỗng nghĩa là
  câu đó vĩnh viễn không khớp route nào. Từ khi cache ghi xuống đĩa, "vĩnh viễn" là đúng nghĩa đen —
  khởi động lại không còn xoá được nó nữa, nên phải kiểm tra cả lúc ghi lẫn lúc nạp từ file.
- **Chuẩn hóa**: khó hơn. Bộ chuẩn hóa fail-open bằng cách trả về *nguyên câu gốc* — mà đó cũng là
  kết quả đúng khi câu vốn đã chuẩn. **Không phân biệt được từ bên ngoài.** Xử lý hiện tại: kết quả
  giống hệt input thì cache TTL ngắn hơn nhiều (10 phút so với 120 phút). Đừng bỏ cơ chế này đi.

### 5.4 Chuẩn hóa làm lệch điểm ~0.10

Node chuẩn hóa trả về câu **viết hoa + có dấu câu** (`"thôi tôi đi đây"` → `"Thôi tôi đi đây."`),
còn câu mẫu viết chữ thường. Chênh lệch này ăn mất khoảng **0.10 điểm cosine kể cả khi nội dung
giống hệt nhau** — từng gây false negative ở ngưỡng 0.84 (đo được 0.83995).

Cải thiện khả dĩ: viết câu mẫu ở dạng đã chuẩn hóa. Sẽ phải nhúng lại các câu đó.

### 5.5 Câu mẫu `out_of_scope` phải tránh chủ đề mà game cũng nói tới

Đã suýt đưa "thời tiết hôm nay ra sao" vào — nhưng game tên *Rainswept*, mưa là chi tiết hiện trường.
`"tối hôm đó trời có mưa không"` là câu hỏi điều tra hợp lệ.

### 5.6 MAX aggregation → câu mẫu gần trùng là vô ích

`"chào bạn"/"chào anh"/"chào chị"/"chào bạn nhé"` chỉ là một câu lặp bốn lần. Thêm câu mẫu **chỉ có
giá trị khi nó phủ một cách nói MỚI**. Cũng tránh: câu mở đầu đứng trước câu hỏi thật
("xin lỗi làm phiền"), và câu bắc cầu giữa hai route ("cảm ơn và tạm biệt").

### 5.6b Vòng phụ thuộc DI khi thêm decorator

`MemoryQueryCache` cần `ModelId`, nhưng KHÔNG được lấy qua `IEmbeddingProvider`:

```
MemoryQueryCache -> IEmbeddingProvider -> CachingEmbeddingProvider -> IQueryCache -> MemoryQueryCache
```

Hiện đang truyền thẳng `string embeddingModelId` từ composition root. Ai thêm decorator mới cần chú ý
cùng cái bẫy này.

### 5.6c `MemoryCache.Keys` là cái bẫy khi cần liệt kê entry

.NET 9 có `MemoryCache.Keys` và nó trông như lời giải hiển nhiên cho việc xuất snapshot đem đi lưu.
Nhưng lấy giá trị ra vẫn phải gọi `TryGetValue`, và việc đó **làm mới sliding expiration của mọi
entry được duyệt qua** — flush định kỳ sẽ khiến không entry nào hết hạn nữa, phá đúng cơ chế TTL
ngắn ở mục 5.3b. Vì vậy `MemoryQueryCache` giữ một `ConcurrentDictionary` song song, đồng bộ qua
`PostEvictionCallback` (bỏ qua `EvictionReason.Replaced` vì callback chạy trên thread pool và có thể
tới sau khi chỉ mục đã ghi entry mới). Đừng "đơn giản hóa" ngược lại.

### 5.7 Không xác minh được Qdrant qua log gRPC

`QdrantClient` khởi tạo bằng host/port nên **không nối vào `ILoggerFactory`** — không sinh dòng log nào.
Cách xác minh đúng: đổi `QDRANT__HOST` sang host không tồn tại, rồi kiểm tra câu tán gẫu vẫn `200`
còn câu hỏi tri thức thì `500`.

### 5.8 Môi trường dev (Windows + Git Bash)

- Python cài trên Windows **không thấy đường dẫn `/tmp` của Git Bash**. Cho Python `print(...)` rồi
  để bash redirect ra file.
- Truyền JSON tiếng Việt inline cho `curl` trong bash sẽ hỏng encoding — ghi ra file rồi
  `--data-binary @file`. Hoặc dùng `json.dumps(..., ensure_ascii=True)`.
- Trên PowerShell phải gọi `curl.exe`, vì `curl` là alias của `Invoke-WebRequest`.
- Đặt `PYTHONIOENCODING=utf-8` khi in tiếng Việt từ Python.

### 5.9 `.env` KHÔNG bị lộ

Đã kiểm chứng: `.env` nằm trong `.gitignore` (dòng 40), **chưa từng có commit nào đụng tới nó**, quét
toàn bộ lịch sử git không thấy chuỗi nào giống API key. (Một phiên trước từng báo động nhầm chuyện này.)

---

## 6. Bản đồ file

Cập nhật sau đợt refactor SOLID (xem `## 9`).

```
RAG/
  Program.cs                        chỉ còn AddRagStack — thứ tự đăng ký nằm trong DI, không ở đây
  appsettings.json                  prompt, ngưỡng, câu mẫu, chunking, thông báo lỗi
  .env                              secret + URL (gitignored)
  Design game.docx                  thiết kế game — đọc khi cần hiểu domain
  Interface/
    IRagPipeline.cs                 IAskService / IIngestionService / IRouteDiagnostics / IRouteAdmin
    IVectorStore.cs                 + VectorRecord, VectorHit, VectorSearchFilter (KHÔNG có kiểu Qdrant)
    ISemanticRouter.cs              Route() đồng bộ + Explain() + AddUtterancesAsync()
    IRouteScorer.cs                 quy tắc gộp điểm nhiều câu mẫu -> một điểm route
    IRouterWarmup.cs                để hosted service không phụ thuộc lớp cụ thể
    IChunkingStrategy.cs / IDocumentTextExtractor.cs
    DocumentSource.cs               + IngestionResult (không dùng IFormFile ở tầng nghiệp vụ)
    DocumentChunk.cs / RouteExplanation.cs
    RouteMatch.cs                   + RouteScore
    RouteUpdateResult.cs            + RouteUpdateStatus (mã, KHÔNG mang câu chữ)
    IRouteVectorCache.cs / IRouteUtteranceStore.cs
    IEmbeddingProvider.cs           ModelId, Dimensions (property, không async), batch
    IQueryCache.cs                  INormalizationCache / IEmbeddingCache /
                                    IQueryCacheStatistics / IPersistableQueryCache
    IQueryCacheStore.cs             + QueryCacheSnapshot
    EmbeddingRateLimitedException.cs   429 -> DỪNG
    EmbeddingUnavailableException.cs   lỗi khác -> 503 (bug §5.3 đã sửa)
  Class/
    RagPipeline.cs                  façade mỏng, mỗi method một dòng ủy quyền
    GeminiEmbeddingProvider.cs      batch + fallback + 429; NÉM thay vì trả mảng rỗng
    GeminiLLMProvider.cs / GroqCloudProvider.cs / KeyedLlmProviderResolver.cs
    Answering/AskPipeline.cs        lõi đường trả lời
    Retrieval/QdrantVectorStore.cs  nơi DUY NHẤT import Qdrant.Client.Grpc (ngoài composition root)
    Ingestion/
      DocumentIngestionService.cs   rút văn bản -> cắt đoạn -> nhúng theo lô -> ghi
      SentenceAwareChunker.cs       thay TextChunker static
      PlainTextExtractor.cs         .txt/.md/.json; thêm PDF = thêm 1 lớp
    Routing/
      EmbeddingSemanticRouter.cs    ~120 dòng, CHỈ so khớp
      RouteCatalog.cs               trạng thái dùng chung + Rebuild (copy-on-write)
      RouteCatalogBuilder.cs        nạp vector, IRouterWarmup
      RouteUtteranceAdmin.cs        thêm câu mẫu lúc chạy
      MaxSimilarityScorer.cs        MAX chứ không phải trung bình — xem §5.6
      RouteDiagnosticsService.cs
      PassthroughSemanticRouter.cs  Null Object
      SemanticRouterWarmupService.cs
      FileRouteVectorCache.cs / NullRouteVectorCache.cs
      FileRouteUtteranceStore.cs / NullRouteUtteranceStore.cs
    Caching/
      MemoryQueryCache.cs           cache RAM có chặn trần / NullQueryCache.cs
      CachingQueryNormalizer.cs     decorator
      CachingEmbeddingProvider.cs   decorator, batch cũng đi qua cache
      FileQueryCacheStore.cs        ghi nhị phân / NullQueryCacheStore.cs
      QueryCachePersistenceService.cs  write-behind: flush định kỳ + lúc tắt
    Config/                         mỗi node một options class, có DataAnnotations
    Constants/                      hằng số giao thức + PayloadFilterMode
    Validation/NotBlankAttribute.cs
  Extension/
    DependencyInjection/            mỗi module một file AddXxx; RagStack khóa thứ tự
    Errors/RagExceptionHandler.cs   exception nghiệp vụ -> mã HTTP
    AtomicFileWriter.cs             ghi file tạm rồi đổi tên, dùng chung 3 kho
    VectorMath.cs                   cosine đầy đủ (KHÔNG giả định vector đã chuẩn hóa L2)
    AppDataPath.cs                  giải đường dẫn theo ContentRootPath
  Controllers/
    QueryController.cs              ask (chỉ nhận IAskService)
    IngestionController.cs          upload, create-collection
    RouteController.cs              route-debug, route-utterances, cache-stats
  RAG.http                          request kiểm thử sẵn
docs/semantic-router-plan.md        kế hoạch + 2 phụ lục kết quả đo
context.md                          file này
```

## 6b. Triển khai trên Render

Render build từ Dockerfile, **không có `docker run -v`**. Dockerfile đã trỏ cả ba file cache vào
`/var/data` qua biến môi trường; trên Dashboard phải gắn một **Disk** với Mount Path = `/var/data`.

Bốn ràng buộc của Render Disk (đã tra tài liệu):

- Disk **chỉ có trên gói trả phí**. Trên gói free, cache đĩa là vô nghĩa — mọi thứ ngoài mount path
  đều ephemeral. Cache RAM vẫn chạy trong vòng đời container.
- Chỉ dữ liệu **dưới mount path** sống qua deploy/restart.
- Disk **chặn deploy không gián đoạn** → có `SIGTERM` → flush lúc tắt chạy được.
- Disk **chặn scale nhiều instance** → loại bỏ hẳn rủi ro nhiều container ghi đè cùng một file.

`.env` **không** có trong image (đã thêm vào `.dockerignore`); secret đặt ở Environment của Render.
Đã kiểm chứng app khởi động bình thường khi thiếu `.env` — `Env.Load()` bỏ qua file không tồn tại.

`RAG.csproj` phải giữ `<Content Remove="App_Data\**" />`: Web SDK gom mọi `*.json` thành Content,
không loại trừ thì cache máy dev bị đóng gói vào image.

---

## 7. Trạng thái hiện tại

- **Build sạch**, 0 warning, 0 error (`dotnet build RAG.slnx`).
- Đã kiểm chứng end-to-end: 4 route khớp đúng (0.838–0.913), câu hỏi trong game đi RAG (0.45–0.66),
  thêm câu mẫu lúc chạy có hiệu lực ngay và sống sót qua restart.
- Đợt refactor SOLID ở `## 9` đã xong, build sạch và chạy thật đạt. Xem mục đó trước khi sửa tiếp.

### Chạy

```bash
dotnet build RAG.slnx
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5263 \
  dotnet run --project RAG --no-build
```

`appsettings.Development.json` đã bật `"RAG": "Debug"` để thấy log quyết định định tuyến.

---

## 8. Việc còn lại / nợ kỹ thuật

Đợt refactor ở `## 9` đã xử lý các mục 1, 4, 6, 8 của danh sách cũ. Còn lại:

1. **Endpoint `route-utterances` chưa có xác thực.** Nếu API mở ra ngoài, người lạ thêm câu mẫu là
   lái được NPC trả lời sai chủ đích.
2. **Chưa có đường xóa câu mẫu đã thêm** — phải sửa `App_Data/route-utterances.json` rồi restart.
3. **Chưa có test.** Vẫn là nợ lớn nhất, và giờ có thêm chỗ đáng test:
   `VectorMath.CosineSimilarity` (ca vector CHƯA chuẩn hóa: `[2,0,0]` vs `[5,0,0]` phải ra 1.0),
   `SentenceAwareChunker` (ca overlap >= chunkSize phải không lặp vô tận), và ngưỡng của router.
   Chưa làm vì người dùng chốt KHÔNG thêm NuGet package nào, mà test project cần xUnit.
4. **160 câu mẫu là bản nháp do Claude viết** — nên thay bằng ngôn ngữ người chơi thật sự dùng.
5. **Phần Docker của cache VẪN CHƯA chạy thử** — Docker Desktop không khởi động được (cả lần trước
   lẫn lần này). Cần tự kiểm chứng `docker run -v rag-cache:/var/data ...` hai lần liên tiếp:
   lần thứ hai phải khởi động mà không gọi Gemini lần nào.
6. **Không có retry/backoff cho HTTP.** Đã đặt timeout qua config (`Gemini:TimeoutSeconds`,
   `GEMINILLM:TimeoutSeconds`), nhưng một lỗi mạng thoáng qua giờ thành 503 thẳng cho người chơi.
   Sửa đúng cần `Microsoft.Extensions.Http.Resilience` — lại vướng ràng buộc không thêm package.

---

## 9. Đợt refactor SOLID (đã xong)

Phạm vi do người dùng chốt: refactor **sâu** (tách lớp, đổi tên), **giữ nguyên cây thư mục gốc**,
**không thêm NuGet package nào**, và sửa bug §5.3 theo hướng **ném exception**.

### Bug đã sửa

| | Trước | Sau |
|---|---|---|
| §5.3 embedding nuốt lỗi HTTP | trả `Array.Empty<float>()` → vector rỗng vào Qdrant → câu trả lời dựng trên ngữ cảnh rác, **không triệu chứng** | ném `EmbeddingUnavailableException` / `EmbeddingRateLimitedException` → **503 / 429** kèm ProblemDetails |
| Hai nguồn sự thật cho số chiều | `QDRANT:Dimensions` **và** `Gemini:OutputDimensions`; `EnsureCollectionExistsAsync` nhận tham số rồi **bỏ qua** | xoá `QDRANT:Dimensions`; số chiều chỉ đến từ `IEmbeddingProvider.Dimensions` |
| `ListCollections` ở MỌI request ask | 1 round-trip gRPC cho 100% traffic | **0** trên đường ask; cổng một-lần trong kho vector cho đường ingest |
| Rò rỉ file tạm khi upload | `Path.GetTempFileName()` không bao giờ xoá | đọc thẳng từ stream của request |
| `.pdf` bị bỏ **im lặng** | nằm trong whitelist nhưng không nhánh nào đọc | bộ đọc tự khai định dạng; file không hỗ trợ được báo rõ trong response |
| Bịa `Guid.NewGuid()` khi id Qdrant hỏng | giấu điểm dữ liệu hỏng | bỏ point đó + log warning |

**Thay đổi có thể phá client:** `POST api/query/ask` giờ trả **429/503** khi Gemini lỗi, thay vì
200 kèm câu trả lời rác. `QDRANT__DIMENSIONS` đã bị xoá khỏi `.env` — **nhớ xoá cả trên Render**.

### SOLID

- **DIP** — `IVectorStore` thay `IQdrantProvider`: interface không còn `using static` chính
  implementation của nó, và pipeline không còn tự dựng `Qdrant.Client.Grpc.Filter`.
  Hai hosted service giờ phụ thuộc `IRouterWarmup` / `IPersistableQueryCache` thay vì lớp cụ thể.
- **SRP** — `EmbeddingSemanticRouter` 474 → ~120 dòng (tách `RouteCatalog`, `RouteCatalogBuilder`,
  `RouteUtteranceAdmin`, `MaxSimilarityScorer`). `RAGPipline` → `RagPipeline` façade mỏng +
  `AskPipeline` / `DocumentIngestionService` / `RouteDiagnosticsService`. `Extension.cs` 335 dòng
  → 11 file theo module. `QueryController` 160 dòng → 3 controller.
- **ISP** — `IQueryCache` tách thành 4 interface; controller đọc số liệu không còn thấy đường ghi.
  Pipeline tách thành 4 vai trò; controller `ask` không có cách nào gọi nhầm `IngestAsync`.
- **OCP** — `IDocumentTextExtractor` + `IChunkingStrategy` (thay `TextChunker` static);
  thêm PDF = thêm một lớp, không sửa controller.

### Hết hardcode

Whitelist định dạng, thông báo lỗi, `Distance.Cosine`, tham số tokenizer, `MatchPhrase`,
hằng số cắt đoạn, `ErrorBodyLogLimit`, timeout HTTP, đường dẫn `/openapi`, và **toàn bộ câu chữ
của endpoint quản trị route** đều đã ra `appsettings.json`. Section mới: `Chunking`, `Ingestion`,
`ErrorResponses`, `RouteMessages`, `OpenApi`.

Ngoài ra: mọi options class được `ValidateDataAnnotations().ValidateOnStart()` — thiếu một biến
môi trường giờ làm app **không khởi động được** kèm thông báo rõ, thay vì chết ở request đầu tiên
bằng `UriFormatException`.

### Đã kiểm chứng

Build 0 warning / 0 error. Chạy thật, so với mốc chụp trước khi sửa:

- **Điểm định tuyến giống hệt tới 6 chữ số thập phân** trên 4 câu kiểm thử (chitchat / thanks /
  farewell / câu hỏi trong game) — đây là bài kiểm tra chính của việc tách router.
- Đường ask đúng cả hai nhánh; upload + truy hồi trả về đúng đoạn vừa nạp.
- Đặt sai `GEMINI__APIKEY` → **503** kèm ProblemDetails (trước đây là 200 kèm câu trả lời rác).
- Xoá `GEMINILLM__URL` → app **không khởi động**, báo đúng tên field thiếu.
- 0 lần `ListCollections` sau nhiều request ask; cache hit tăng khi hỏi lại; thư mục temp không
  sinh file rác sau upload.
- **Chưa kiểm chứng được:** phần Docker (xem mục 5 ở trên).
