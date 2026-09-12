FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY RAG/RAG.csproj RAG/
RUN dotnet restore RAG/RAG.csproj

COPY RAG/ RAG/
WORKDIR /src/RAG
RUN dotnet publish RAG.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# ---------------------------------------------------------------------------
# Thu muc du lieu ben vung: cache vector cau mau, cau mau them luc chay, cache
# hoi dap, va cache cau tra loi cua provider FAISS. Khong co no thi moi lan deploy
# phai nhung lai toan bo cau mau (~2-3 phut) va tra lai tu dau do tre lan quota
# Gemini cho tung cau hoi.
#
# TREN RENDER: phai gan mot Disk (Dashboard > service > Disks) va dat
#   Mount Path = /var/data
# Chi du lieu nam DUOI mount path moi song qua deploy va restart; toan bo phan
# con lai cua filesystem la ephemeral. Disk yeu cau instance type TRA PHI —
# tren goi free khong gan duoc Disk, va khi do cache chi song trong RAM cua
# container dang chay.
#
# Danh doi khi gan Disk (theo tai lieu Render):
#   - Mat kha nang deploy khong gian doan: Render phai dung instance cu truoc
#     khi khoi dong instance moi.
#   - Khong scale ra nhieu instance duoc: mot Disk chi gan duoc cho mot instance.
#     Voi cache dung chung thi day lai la dieu tot — no loai bo han kha nang
#     nhieu container ghi de len cung mot file.
#
# Doi mount path khac thi ghi de bon bien duoi day trong Environment cua Render.
# ---------------------------------------------------------------------------
# LUU Y ve ten bien: hai bien cua router nam trong section con Embedding. Dat nham ten cu
# (SemanticRouter__VectorCachePath) thi app TU CHOI khoi dong kem thong bao chi ro ten moi — co y
# nhu vay, vi bind hut se im lang day cache ra ngoai Disk va nhung lai 160 cau mau moi lan deploy.
ENV SemanticRouter__Embedding__VectorCachePath=/var/data/route-vectors.json
ENV SemanticRouter__Embedding__UtteranceStorePath=/var/data/route-utterances.json
ENV QueryCache__PersistPath=/var/data/query-cache.bin
ENV SemanticAnswerCache__Faiss__PersistPath=/var/data/answer-cache.bin

# Tao san thu muc de app van chay binh thuong khi CHUA gan Disk.
# Luc do /var/data chi la thu muc thuong trong container: ghi doc deu duoc,
# nhung mat sau moi lan deploy.
RUN mkdir -p /var/data

# ---------------------------------------------------------------------------
# CACHE CAU TRA LOI THEO NGU NGHIA - chon provider bang SemanticAnswerCache__Provider
#
# Tang nay co HAI cai dat, va chung doi hoi ha tang KHAC NHAU. Dung ket luan tu mot cai roi ap
# cho cai kia:
#
#   SemanticAnswerCache__Provider=Faiss  (mac dinh)
#     Chay TRONG tien trinh, khong can dich vu ngoai nao. Doi lai cache nam trong RAM cua MOT
#     tien trinh:
#       - Ben vung PHU THUOC VAO DISK gan o /var/data (bien o tren). KHONG gan Disk thi app van
#         chay binh thuong, chi la cache rong lai sau moi lan deploy.
#       - Scale ra nhieu instance thi MOI INSTANCE MOT CACHE RIENG, va hai instance cung ghi de
#         len mot file neu dung chung Disk. Can dung chung cache thi phai doi sang Redis.
#     Native cua FAISS co san cho linux-x64 va linux-arm64 nen image aspnet:9.0 tren ca hai kien
#     truc deu chay.
#
#   SemanticAnswerCache__Provider=Redis
#     Cache nam NGOAI tien trinh nen KHONG lien quan gi toi /var/data, va nhieu instance dung
#     chung duoc. Doi lai phai co mot Redis 8+ CO module search:
#       - Chay chung docker-compose voi service `redis`: host la TEN SERVICE, khong phai localhost
#           SemanticAnswerCache__Redis__ConnectionString=redis:6379
#       - Tren Render: phai la mot Redis MANAGED co module search (Redis Cloud free tier co).
#           localhost:6379 khong ton tai o do.
#
# LUU Y ve ten bien: khoa cua Redis da doi tu dang PHANG sang section con khi tang nay tach thanh
# hai provider (SemanticAnswerCache__ConnectionString -> SemanticAnswerCache__Redis__ConnectionString).
# Con dat ten cu thi app TU CHOI khoi dong kem thong bao chi ro ten moi - co y nhu vay, vi bind hut
# se roi ve localhost:6379 va luat fail-open cua chinh tang nay nuot sach loi ket noi.
#
# Mac dinh trong appsettings.json la Enabled=false, nen khong dat bien thi app chay binh thuong
# voi Null Object - khong co cache, khong co loi.
# ---------------------------------------------------------------------------

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RAG.dll"]
