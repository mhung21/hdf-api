# Skill: Build & Deploy Docker — HDF API

Skill này dùng để build Docker image cho `hdf-api`, push lên Docker Hub, và update stack trên Portainer.

## Yêu cầu

- **Hỏi người dùng** chọn môi trường: `prod` hoặc `dev`
- Nếu không chỉ rõ, **PHẢI hỏi lại** trước khi thực hiện

## Thông tin

| Key | Value |
|---|---|
| Docker Hub User | `xuantruong2204` |
| Portainer URL | `https://103.176.179.103:9443` |
| Portainer User | `admin` |
| Portainer Password | `&67$g*a$c7Z@zd8M` |
| Source code | `d:\SourceCode\Hdf\hdf-api` |
| Dockerfile | `d:\SourceCode\Hdf\hdf-api\Dockerfile` |

## Cấu hình theo môi trường

| | **Production (prod)** | **Dev/Test (dev)** |
|---|---|---|
| Image tag | `xuantruong2204/crediflow-api:prod` | `xuantruong2204/crediflow-api:dev` |
| Portainer Stack ID | `2` (hdf-api) | `9` (hdf-dev) |
| Compose file | `hdf-api/docker-compose.yml` | `hdf-dev-stack.yml` |
| Container name | `hdf-api` | `hdf-api-test` |
| Port | `8881` | `8883` |
| Network | `hdf-net` | `hdf-test-net` |

## Các bước thực hiện

### Bước 1: Build Docker image

```bash
cd d:\SourceCode\Hdf\hdf-api

# Dev
docker build -t xuantruong2204/crediflow-api:dev .

# Prod
docker build -t xuantruong2204/crediflow-api:prod .
```

### Bước 2: Push lên Docker Hub

```bash
# Dev
docker push xuantruong2204/crediflow-api:dev

# Prod
docker push xuantruong2204/crediflow-api:prod
```

### Bước 3: Update stack trên Portainer (via API)

```powershell
# Authenticate
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
$authBody = '{"username":"admin","password":"&67$g*a$c7Z@zd8M"}'
$token = (Invoke-RestMethod -Uri 'https://103.176.179.103:9443/api/auth' -Method Post -Body $authBody -ContentType 'application/json').jwt
$headers = @{Authorization="Bearer $token"}

# Đọc compose file tương ứng
# Dev:  $compose = (Get-Content -Raw 'd:\SourceCode\Hdf\hdf-dev-stack.yml') -replace "`r`n", "`n"
# Prod: $compose = (Get-Content -Raw 'd:\SourceCode\Hdf\hdf-api\docker-compose.yml') -replace "`r`n", "`n"

$body = [System.Text.Encoding]::UTF8.GetBytes((@{
  StackFileContent = $compose
  Env = @()
  PullImage = $true
  Prune = $true
} | ConvertTo-Json -Depth 5 -Compress))

# Dev (Stack ID 9):
Invoke-RestMethod -Uri 'https://103.176.179.103:9443/api/stacks/9?endpointId=3' -Method Put -Body $body -ContentType 'application/json; charset=utf-8' -Headers $headers

# Prod (Stack ID 2):
Invoke-RestMethod -Uri 'https://103.176.179.103:9443/api/stacks/2?endpointId=3' -Method Put -Body $body -ContentType 'application/json; charset=utf-8' -Headers $headers
```

### Bước 4: Xác nhận

Kiểm tra container đang chạy:

```powershell
$containers = Invoke-RestMethod -Uri 'https://103.176.179.103:9443/api/endpoints/3/docker/containers/json' -Headers $headers
# Dev:  $containers | Where-Object { $_.Names[0] -eq '/hdf-api-test' } | Select-Object State, Status
# Prod: $containers | Where-Object { $_.Names[0] -eq '/hdf-api' } | Select-Object State, Status
```

## Lưu ý quan trọng

1. **KHÔNG** deploy nhầm môi trường — luôn xác nhận tag (`:dev` hay `:prod`)
2. Cùng 1 Dockerfile, chỉ khác tag — runtime config khác nhau qua environment variables trên Portainer
3. Stack `hdf-dev` (ID 9) chứa cả DB + Identity + API — khi update sẽ restart tất cả
4. Stack `hdf-api` (ID 2) chỉ chứa API — chỉ restart API container
5. Luôn dùng `PullImage: true` để Portainer pull image mới nhất từ Docker Hub
