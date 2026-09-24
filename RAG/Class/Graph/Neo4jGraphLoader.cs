using Microsoft.Extensions.Options;
using Neo4j.Driver;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Nạp dữ liệu đồ thị viết tay lên Neo4j, và đối chiếu số đếm.
    /// <para>
    /// Toàn bộ là <c>MERGE</c> + <c>SET</c>, KHÔNG có <c>DETACH DELETE</c> ở đâu: chạy lại bao nhiêu
    /// lần cũng ra một kết quả, không cần xóa database. Cái giá là MERGE không bao giờ xóa — gỡ một
    /// cạnh khỏi file nguồn thì phải xóa tay, và <see cref="VerifyAsync"/> chính là thứ báo cho biết
    /// database đang nhiều hơn file.
    /// </para>
    /// <para>
    /// Phân quyền KHÔNG đọc từ file mà suy từ front matter của corpus qua cùng một
    /// <see cref="ICorpusIngestionService"/> đã đặt <c>npcNames</c> vào payload Qdrant. Đó là điểm
    /// then chốt: hai nhánh phân quyền độc lập thì chỉ cần một bên sót là tri thức rò sang NPC khác
    /// mà không gì báo. Một hàm thì không lệch được.
    /// </para>
    /// </summary>
    public sealed class Neo4jGraphLoader : IGraphLoader, IGraphSchemaAdmin
    {
        private readonly IDriver _driver;
        private readonly IGraphDataSource _dataSource;
        private readonly ICorpusIngestionService _corpus;
        private readonly IOntology _ontology;
        private readonly Neo4jConfig _config;
        private readonly GraphSchemaConfig _schema;
        private readonly ILogger<Neo4jGraphLoader> _logger;

        public Neo4jGraphLoader(IDriver driver,
                                IGraphDataSource dataSource,
                                ICorpusIngestionService corpus,
                                IOntology ontology,
                                IOptions<Neo4jConfig> options,
                                IOptions<GraphSchemaConfig> schema,
                                ILogger<Neo4jGraphLoader> logger)
        {
            _driver = driver;
            _dataSource = dataSource;
            _corpus = corpus;
            _ontology = ontology;
            _config = options.Value;
            _schema = schema.Value;
            _logger = logger;
        }

        public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            // Constraint UNIQUE phải có TRƯỚC lần nạp đầu tiên, không phải sau: MERGE không
            // constraint vẫn có thể đẻ node trùng dưới đồng thời, và Neo4j từ chối thêm constraint
            // hồi tố một khi đã có bản trùng — lúc đó phải dọn tay.
            var statements = new[]
            {
                string.Format(GraphCypher.CreateNameConstraintTemplate, _schema.EntityNameConstraint, _ontology.BaseLabel),
                string.Format(GraphCypher.CreateNpcIndexTemplate, _schema.NpcNameIndex, _ontology.NpcLabel),
                string.Format(GraphCypher.CreateFulltextNameTemplate, _schema.FulltextNameIndex, _ontology.BaseLabel),
                string.Format(GraphCypher.CreateFulltextAliasTemplate, _schema.FulltextAliasIndex, _ontology.BaseLabel)
            };

            await WriteAsync(async runner =>
            {
                foreach (var statement in statements)
                    await (await runner.RunAsync(statement)).ConsumeAsync();

                return 0;
            }, cancellationToken);

            _logger.LogInformation("Đã đảm bảo constraint và index của đồ thị tri thức.");
        }

        public async Task<GraphLoadReport> LoadAsync(CancellationToken cancellationToken = default)
        {
            // Kiểm TOÀN BỘ trước, ghi sau. Ném ở đây nghĩa là chưa một dòng nào chạm Neo4j.
            var data = await _dataSource.LoadAsync(cancellationToken);
            var access = await _corpus.DescribeAccessAsync(cancellationToken);

            var warnings = new List<string>();

            var entityRows = data.Entities.Select(entity => new
            {
                labels = LabelSuffix(entity.Labels),
                row = (object)new Dictionary<string, object>
                {
                    [GraphProperties.Name] = entity.Name,
                    ["props"] = BuildEntityProperties(entity)
                }
            }).ToList();

            var mirrored = data.Relationships
                .Where(relationship => _ontology.Relations[relationship.Type].Symmetric)
                .Select(relationship => relationship with { From = relationship.To, To = relationship.From })
                .ToList();

            // Nguồn giữ MỘT chiều, bộ nạp phát hai chiều. Một chỗ để sửa nên hai chiều không lệch
            // nhau được — và truy vấn đi từ phía nào cũng thấy sự thật đó.
            var allRelationships = data.Relationships.Concat(mirrored).ToList();

            var accessRows = BuildAccessRows(access, ResolveNpcNames(data, access), warnings);

            await WriteAsync(async runner =>
            {
                foreach (var group in entityRows.GroupBy(entity => entity.labels))
                {
                    var statement = string.Format(GraphCypher.MergeEntitiesTemplate, _ontology.BaseLabel, group.Key);

                    await (await runner.RunAsync(statement, new Dictionary<string, object>
                    {
                        [GraphCypher.Parameters.Rows] = group.Select(entity => entity.row).ToList()
                    })).ConsumeAsync();
                }

                foreach (var group in allRelationships.GroupBy(relationship => relationship.Type))
                {
                    // Loại quan hệ lấy từ ontology ĐÃ KIỂM, không bao giờ từ dòng dữ liệu: đây là
                    // định danh được ghép thẳng vào câu lệnh, nên lấy từ dữ liệu là mở đường cho
                    // một dòng JSON chèn Cypher tùy ý.
                    var type = _ontology.Relations[group.Key].Name;
                    var statement = string.Format(GraphCypher.MergeRelationshipsTemplate, _ontology.BaseLabel, type);

                    await (await runner.RunAsync(statement, new Dictionary<string, object>
                    {
                        [GraphCypher.Parameters.Rows] = group.Select(BuildRelationshipRow).ToList()
                    })).ConsumeAsync();
                }

                if (accessRows.Count > 0)
                {
                    var statement = string.Format(GraphCypher.MergeAccessTemplate, _ontology.BaseLabel, _ontology.NpcLabel);

                    await (await runner.RunAsync(statement, new Dictionary<string, object>
                    {
                        [GraphCypher.Parameters.Rows] = accessRows
                    })).ConsumeAsync();
                }

                return 0;
            }, cancellationToken);

            _logger.LogInformation("Đã nạp đồ thị: {Entities} thực thể, {Relationships} cạnh ({Mirrored} cạnh ngược), {Access} NPC.",
                data.Entities.Count, allRelationships.Count, mirrored.Count, accessRows.Count);

            return new GraphLoadReport(data.Entities.Count, allRelationships.Count, mirrored.Count, accessRows.Count, warnings);
        }

        public async Task<GraphVerifyReport> VerifyAsync(CancellationToken cancellationToken = default)
        {
            var data = await _dataSource.LoadAsync(cancellationToken);

            // Số mong đợi = cạnh gold CỘNG số cạnh đối xứng. Quên vế sau là báo lệch oan ở mọi lần
            // đối chiếu, và rồi không ai còn tin vào phép đối chiếu nữa.
            var expected = data.Relationships.Count +
                           data.Relationships.Count(relationship => _ontology.Relations[relationship.Type].Symmetric);

            var counts = await ReadAsync(async runner =>
            {
                var entities = await Scalar(runner, string.Format(GraphCypher.CountEntitiesTemplate, _ontology.BaseLabel));
                var npcs = await Scalar(runner, string.Format(GraphCypher.CountNpcsTemplate, _ontology.NpcLabel));
                var relationships = await Scalar(runner, GraphCypher.CountRelationships);

                var byType = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var record in await (await runner.RunAsync(GraphCypher.CountByRelationType)).ToListAsync())
                {
                    byType[record[GraphCypher.Columns.Relation].As<string>()] =
                        record[GraphCypher.Columns.Count].As<int>();
                }

                return (entities, npcs, relationships, byType);
            }, cancellationToken);

            var matches = counts.relationships == expected && counts.entities == data.Entities.Count;

            if (!matches)
            {
                // Lệch không phải lỗi: MERGE không xóa, nên một cạnh đã gỡ khỏi file vẫn còn trong
                // database cho tới khi có người xóa tay. Ghi Warning để chuyện đó hiện ra chứ không
                // âm thầm trôi qua nhiều lần deploy.
                _logger.LogWarning("Đồ thị lệch với file nguồn: {Relationships} cạnh trong DB so với {Expected} mong đợi, " +
                                   "{Entities} thực thể so với {ExpectedEntities}. MERGE không xóa, nên phần thừa phải xóa tay.",
                    counts.relationships, expected, counts.entities, data.Entities.Count);
            }

            return new GraphVerifyReport(counts.entities, counts.npcs, counts.relationships, expected, counts.byType, matches);
        }

        /// <summary>
        /// Chuỗi nhãn phụ dạng <c>:A:B</c>, LUÔN lấy từ ontology đã kiểm.
        /// <para>
        /// Bỏ qua nhãn nền: nó đã có trong mệnh đề <c>MERGE</c>. Nhãn lạ thì bỏ qua thay vì ghép
        /// vào — tới đây thì <see cref="IGraphDataSource"/> đã chặn rồi, nên đây chỉ là lớp cuối
        /// cùng giữ cho không một chuỗi nào ngoài ontology lọt vào câu lệnh.
        /// </para>
        /// </summary>
        private string LabelSuffix(IReadOnlyList<string> labels) =>
            string.Concat(labels
                .Where(label => label != _ontology.BaseLabel && _ontology.Labels.Contains(label))
                .Select(label => ":" + _ontology.Labels.First(known => known == label)));

        private static Dictionary<string, object> BuildEntityProperties(GraphEntityData entity)
        {
            var properties = new Dictionary<string, object>(StringComparer.Ordinal);

            // Cùng thứ tự với BuildRelationshipRow và cùng lý do: nguon_chunk là thứ mà cả ba tầng
            // lọc quyền đọc để quyết định NPC nào thấy được node này. Ghi nó SAU thuoc_tinh nghĩa là
            // một khóa trùng tên trong dữ liệu không nới được quyền của chính mình.
            foreach (var (key, value) in entity.Properties)
                properties[key] = value;

            properties[GraphProperties.SourceChunks] = entity.ChunkCodes.ToList();

            if (entity.Aliases.Count > 0)
                properties[GraphProperties.Aliases] = entity.Aliases.ToList();

            return properties;
        }

        private static object BuildRelationshipRow(GraphRelationshipData relationship)
        {
            var properties = new Dictionary<string, object>(StringComparer.Ordinal);

            // Thuộc tính của người soạn ghi TRƯỚC để hai khóa hệ thống bên dưới luôn thắng: một dòng
            // dữ liệu đặt trang_thai trong thuoc_tinh sẽ đè lên giá trị đã qua validator, và đó là
            // đường vòng qua đúng phép kiểm giữ cho tin đồn không được trình bày như kết luận.
            foreach (var (key, value) in relationship.Properties)
                properties[key] = value;

            properties[GraphProperties.Status] = relationship.Status;
            properties[GraphProperties.SourceChunks] = relationship.ChunkCodes.ToList();

            // Ghi phu_dinh trên MỌI cạnh, kể cả khi false. Ghi có điều kiện thì một cạnh từng phủ
            // định mà nay không còn sẽ giữ lại `phu_dinh: true` cũ — MERGE không xóa thuộc tính, và
            // nghĩa của cạnh lặng lẽ đảo ngược so với file nguồn.
            properties[GraphProperties.Negated] = relationship.Negated;

            return new Dictionary<string, object>
            {
                ["tu"] = relationship.From,
                ["den"] = relationship.To,
                ["props"] = properties
            };
        }

        /// <summary>
        /// Gom quyền theo NPC: mỗi NPC nhận danh sách MÃ TÀI LIỆU được đọc.
        /// <para>
        /// NPC có trong corpus mà không có trong <c>entities.json</c> thì chỉ CẢNH BÁO chứ không
        /// ném, nhưng câu lệnh dùng <c>MATCH</c> nên nó sẽ không được ghi — cố tình. <c>MERGE</c> ở
        /// đó sẽ âm thầm tạo node thứ hai khi tên lệch một dấu tiếng Việt, và NPC ấy mất sạch tri
        /// thức domain trong khi mọi thứ vẫn chạy.
        /// </para>
        /// </summary>
        /// <summary>
        /// Nối tên NPC trong front matter corpus với tên thực thể trong <c>entities.json</c>, qua
        /// <c>bi_danh</c> khi hai bên gọi khác nhau.
        /// <para>
        /// Corpus ghi VAI TRÒ (<c>nguon: "Cảnh sát 1, Cảnh sát 2"</c>) còn đồ thị mang TÊN RIÊNG
        /// (<c>James</c>, <c>Michael</c>). Thường thì <c>IAccessPolicy</c> đã đổi sẵn sang tên riêng
        /// qua <c>INpcNameResolver</c>; bước nối ở đây là lưới an toàn cho tên nào còn sót. Cầu nối là <c>bi_danh</c>, nên đổi tên một NPC
        /// chỉ phải sửa một dòng trong <c>entities.json</c> — không đụng corpus, không đụng code.
        /// </para>
        /// <para>
        /// Trả về ánh xạ <c>tên trong corpus → tên thực thể</c>. Tên nào không nối được thì vắng mặt
        /// ở đây và sẽ thành một cảnh báo, vì quyền của NPC đó sẽ không được ghi.
        /// </para>
        /// </summary>
        private Dictionary<string, string> ResolveNpcNames(GraphData data,
                                                           IReadOnlyList<CorpusDocumentReport> access)
        {
            var fromCorpus = access.SelectMany(document => document.NpcNames).ToHashSet(StringComparer.Ordinal);
            var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entity in data.Entities)
            {
                if (entity.Labels.Contains(_ontology.NpcLabel))
                    resolved.TryAdd(entity.Name, entity.Name);

                foreach (var name in entity.Aliases.Prepend(entity.Name))
                {
                    if (fromCorpus.Contains(name))
                        resolved.TryAdd(name, entity.Name);
                }
            }

            return resolved;
        }

        private static List<object> BuildAccessRows(IReadOnlyList<CorpusDocumentReport> access,
                                                    IReadOnlyDictionary<string, string> npcNames,
                                                    List<string> warnings)
        {
            var byNpc = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var document in access)
            {
                foreach (var npc in document.NpcNames)
                {
                    // Gom theo TÊN THỰC THỂ chứ không theo tên trong corpus: hai vai trò cùng trỏ về
                    // một nhân vật thì quyền phải hợp lại, không phải ghi đè lẫn nhau.
                    if (!npcNames.TryGetValue(npc, out var entity))
                        continue;

                    if (!byNpc.TryGetValue(entity, out var documents))
                        byNpc[entity] = documents = new List<string>();

                    if (!documents.Contains(document.DocId, StringComparer.Ordinal))
                        documents.Add(document.DocId);
                }
            }

            warnings.AddRange(access
                .SelectMany(document => document.NpcNames)
                .Distinct(StringComparer.Ordinal)
                .Where(npc => !npcNames.ContainsKey(npc))
                .Select(npc => $"NPC '{npc}' có trong corpus nhưng entities.json không có thực thể nào mang tên " +
                               "hoặc bí danh đó; quyền của NPC này sẽ KHÔNG được ghi. Kiểm lại dấu tiếng Việt."));

            return byNpc
                .Select(entry => (object)new Dictionary<string, object>
                {
                    [GraphProperties.Name] = entry.Key,
                    [GraphProperties.KnownDocs] = entry.Value
                })
                .ToList();
        }

        private static async Task<int> Scalar(IAsyncQueryRunner runner, string statement)
        {
            var records = await (await runner.RunAsync(statement)).ToListAsync();

            return records.Count == 0 ? 0 : records[0][GraphCypher.Columns.Count].As<int>();
        }

        /// <summary>
        /// Đường GHI: khác đường đọc của <see cref="Neo4jGraphStore"/> ở chỗ nó ĐƯỢC PHÉP ném. Đây
        /// là thao tác quản trị có người đang đứng nhìn kết quả, nên nuốt lỗi ở đây là giấu đi đúng
        /// thứ người ta cần biết.
        /// </summary>
        private async Task<T> WriteAsync<T>(Func<IAsyncQueryRunner, Task<T>> work, CancellationToken cancellationToken)
        {
            try
            {
                await using var session = _driver.AsyncSession(builder => builder.WithDatabase(_config.Database));

                return await session.ExecuteWriteAsync(work).WaitAsync(WriteTimeout, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Translate(exception);
            }
        }

        private async Task<T> ReadAsync<T>(Func<IAsyncQueryRunner, Task<T>> work, CancellationToken cancellationToken)
        {
            try
            {
                await using var session = _driver.AsyncSession(builder => builder
                    .WithDatabase(_config.Database)
                    .WithDefaultAccessMode(AccessMode.Read));

                return await session.ExecuteReadAsync(work).WaitAsync(WriteTimeout, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Translate(exception);
            }
        }

        /// <summary>
        /// Phân biệt "không tới được server" với "server từ chối câu lệnh".
        /// <para>
        /// Gộp hai thứ này là đổ oan cho cấu hình: một xung đột schema hay một lỗi cú pháp sẽ hiện
        /// ra dưới dạng "kiểm lại NEO4J__PASSWORD", và người vận hành đi tìm lỗi ở chỗ không có lỗi.
        /// <c>ClientException</c> nghĩa là kết nối HOÀN TOÀN ổn — Neo4j đã đọc câu lệnh và nói
        /// không. Nguyên văn câu nó trả về được giữ lại vì đó chính là thứ cần đọc.
        /// </para>
        /// </summary>
        private static Exception Translate(Exception exception) => exception switch
        {
            ClientException client => new GraphStatementRejectedException(client.Message, client),
            _ => new GraphUnavailableException(UnavailableMessage, exception)
        };

        /// <summary>
        /// Trần thời gian RỘNG hơn hẳn đường trả lời, và có lý do: nạp cả đồ thị là hàng chục câu
        /// lệnh, còn Aura Free vừa thức dậy sau nhiều ngày ngủ có thể mất hàng chục giây cho câu
        /// đầu tiên. Dùng lại trần 1.5 giây của đường trả lời ở đây là tự làm hỏng mọi lần nạp đầu.
        /// </summary>
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromMinutes(2);

        private const string UnavailableMessage =
            "Không nói chuyện được với Neo4j. Kiểm NEO4J__URI / NEO4J__USERNAME / NEO4J__PASSWORD, " +
            "và nếu dùng Aura Free thì kiểm xem instance có đang ngủ hay không.";
    }
}
