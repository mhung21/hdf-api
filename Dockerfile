# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj trước để tận dụng Docker layer cache khi restore
COPY ["CrediFlow.API/CrediFlow.API.csproj", "CrediFlow.API/"]

# Copy các DLL trong LIB trước khi restore (HintPath cần tồn tại)
COPY ["CrediFlow.API/LIB/", "CrediFlow.API/LIB/"]

RUN dotnet restore "CrediFlow.API/CrediFlow.API.csproj"

# Copy toàn bộ source và publish
COPY . .
RUN dotnet publish "CrediFlow.API/CrediFlow.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
    
# Tạo thư mục lưu file hợp đồng
RUN mkdir -p /data/contracts

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "CrediFlow.API.dll"]
