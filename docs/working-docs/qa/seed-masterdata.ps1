#Requires -Version 7
<#
.SYNOPSIS
    Seed WMS master data (warehouses + locations + products) untuk E2E testing yang berulang.

.DESCRIPTION
    Master data WMS TIDAK di-seed oleh migrations (by-design: hanya `admin`, ADR-0010/0012). Script ini
    mengisi master data baseline via REST gateway supaya tiap sesi test punya data konsisten tanpa klik UI.

    IDEMPOTENT: cek dulu yang sudah ada (warehouse by Name, location by Code, product by SKU) → hanya buat
    yang belum ada. Aman di-run berkali-kali pada stack yang sama.

    Data dirancang untuk COMPLETE E2E (bukan happy-path saja):
      - 2 warehouse (WH1, WH2) → uji multi-warehouse / warehouse-scoping.
      - Lokasi 4 tipe per warehouse → Receiving (GR landing), Quarantine (QcHold), Rack (putaway),
        Staging (picking). WH1 punya 2 rack (putaway override).
      - 4 produk meliputi matriks tracking: batch+expiry, batch+expiry+QC, non-tracking, batch-only.

.PARAMETER Gateway
    Base URL gateway (default https://localhost:58906). Lihat AppHost stdout / Aspire dashboard bila beda.

.EXAMPLE
    pwsh ./docs/working-docs/qa/seed-masterdata.ps1
    pwsh ./docs/working-docs/qa/seed-masterdata.ps1 -Gateway https://localhost:58906
#>
param(
    [string]$Gateway = 'https://localhost:58906',
    [string]$User = 'admin',
    [string]$Password = 'ChangeMe123!'
)
$ErrorActionPreference = 'Stop'

# --- login (retry sampai auth+gateway siap) ---
$token = $null
for ($i = 0; $i -lt 30; $i++) {
    try {
        $login = Invoke-RestMethod -Uri "$Gateway/auth/login" -Method Post -SkipCertificateCheck `
            -ContentType 'application/json' -Body (@{ username = $User; password = $Password } | ConvertTo-Json)
        $token = $login.accessToken; break
    } catch { Start-Sleep -Seconds 2 }
}
if (-not $token) { throw "Auth belum siap di $Gateway — stack sudah `dotnet run` AppHost & Healthy?" }

function Get-Json([string]$path) {
    Invoke-RestMethod -Uri "$Gateway$path" -Headers @{ Authorization = "Bearer $token" } -SkipCertificateCheck
}
function New-Entity([string]$path, $body) {
    # mutating REST butuh Idempotency-Key (ADR auditing)
    $headers = @{ Authorization = "Bearer $token"; 'Idempotency-Key' = [guid]::NewGuid().ToString() }
    Invoke-RestMethod -Uri "$Gateway$path" -Method Post -Headers $headers -SkipCertificateCheck `
        -ContentType 'application/json' -Body ($body | ConvertTo-Json)
}

# --- warehouses (idempotent by Name) ---
$existingWh = (Get-Json '/warehouses').items
$whIds = @{}
foreach ($name in @('WH1', 'WH2')) {
    $found = $existingWh | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if ($found) { $whIds[$name] = $found.warehouseId; Write-Host "= warehouse $name ($($found.warehouseId))" }
    else {
        $created = New-Entity '/warehouses' @{ name = $name; address = "$name - default address" }
        $whIds[$name] = $created.id; Write-Host "+ warehouse $name ($($created.id))" -ForegroundColor Green
    }
}

# --- locations per warehouse (idempotent by Code) ---
$locPlan = [ordered]@{
    WH1 = @(
        @{ code = 'REC-01';  type = 'ReceivingArea'  },
        @{ code = 'QC-01';   type = 'QuarantineArea' },
        @{ code = 'RACK-A1'; type = 'Rack'           },
        @{ code = 'RACK-A2'; type = 'Rack'           },
        @{ code = 'STG-01';  type = 'StagingArea'    }
    )
    WH2 = @(
        @{ code = 'REC-02';  type = 'ReceivingArea'  },
        @{ code = 'QC-02';   type = 'QuarantineArea' },
        @{ code = 'RACK-B1'; type = 'Rack'           },
        @{ code = 'STG-02';  type = 'StagingArea'    }
    )
}
foreach ($whName in $locPlan.Keys) {
    $whId = $whIds[$whName]
    $existingCodes = @((Get-Json "/locations?page=1&pageSize=200&warehouseId=$whId").items | ForEach-Object { $_.code })
    foreach ($loc in $locPlan[$whName]) {
        if ($existingCodes -contains $loc.code) { Write-Host "= location $($loc.code) @ $whName"; continue }
        New-Entity '/locations' @{ warehouseId = $whId; type = $loc.type; code = $loc.code } | Out-Null
        Write-Host "+ location $($loc.code) ($($loc.type)) @ $whName" -ForegroundColor Green
    }
}

# --- products (idempotent by SKU) — matriks tracking lengkap ---
$existingSkus = @((Get-Json '/products?page=1&pageSize=200').items | ForEach-Object { $_.sku })
$products = @(
    @{ sku = 'SUGAR-1KG'; name = 'Refined Sugar 1KG'; uom = 'PIECE';  batch = $true;  expiry = $true;  qc = $false; shelf = 365 },   # batch+expiry → FEFO multi-lot, happy
    @{ sku = 'BEEF-500G'; name = 'Frozen Beef 500g';  uom = 'PIECE';  batch = $true;  expiry = $true;  qc = $true;  shelf = 180 },   # +QC → QcHold/quarantine path
    @{ sku = 'NAIL-CTN';  name = 'Nails 100/ctn';     uom = 'CARTON'; batch = $false; expiry = $false; qc = $false; shelf = $null }, # non-tracking → Over/Short delivery control
    @{ sku = 'RICE-25KG'; name = 'Rice 25KG sack';    uom = 'SACK';   batch = $true;  expiry = $false; qc = $false; shelf = $null }  # batch-only (no expiry) combo
)
foreach ($p in $products) {
    if ($existingSkus -contains $p.sku) { Write-Host "= product $($p.sku)"; continue }
    New-Entity '/products' @{
        sku = $p.sku; name = $p.name; uom = $p.uom
        batchTrackingRequired = $p.batch; expiryTrackingRequired = $p.expiry
        qcRequiredOnReceipt = $p.qc; shelfLifeDays = $p.shelf
    } | Out-Null
    Write-Host "+ product $($p.sku)" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Seed selesai ===" -ForegroundColor Cyan
Write-Host "WH1 = $($whIds['WH1'])"
Write-Host "WH2 = $($whIds['WH2'])"
Write-Host "(UI sekarang pakai dropdown warehouse/SKU — GUID tak perlu diketik manual.)"
