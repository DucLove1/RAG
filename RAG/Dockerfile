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
# Thu muc du lieu ben vung: cache vector cau mau, cau mau them luc chay, va
# cache hoi dap. Khong co no thi moi lan deploy phai nhung lai toan bo cau mau
# (~2-3 phut) va tra lai tu dau do tre lan quota Gemini cho tung cau hoi.
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
# Doi mount path khac thi ghi de ba bien duoi day trong Environment cua Render.
# ---------------------------------------------------------------------------
ENV SemanticRouter__VectorCachePath=/var/data/route-vectors.json
ENV SemanticRouter__UtteranceStorePath=/var/data/route-utterances.json
ENV QueryCache__PersistPath=/var/data/query-cache.bin

# Tao san thu muc de app van chay binh thuong khi CHUA gan Disk.
# Luc do /var/data chi la thu muc thuong trong container: ghi doc deu duoc,
# nhung mat sau moi lan deploy.
RUN mkdir -p /var/data

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RAG.dll"]
