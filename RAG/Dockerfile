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

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RAG.dll"]