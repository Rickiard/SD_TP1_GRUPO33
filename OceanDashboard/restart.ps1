Write-Host "Parando processos existentes..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "dotnet*" -or $_.ProcessName -like "OceanDashboard*" } | ForEach-Object { 
    try {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        Write-Host "Processo $($_.ProcessName) (ID: $($_.Id)) finalizado." -ForegroundColor Green
    } catch {
        Write-Host "Erro ao finalizar processo $($_.ProcessName) (ID: $($_.Id))" -ForegroundColor Red
    }
}

Write-Host "Limpando arquivos de banco de dados..." -ForegroundColor Yellow
$files = @(
    "dados_recebidos.db",
    "dados_recebidos.db-shm",
    "dados_recebidos.db-wal"
)

foreach ($file in $files) {
    $path = Join-Path -Path $PSScriptRoot -ChildPath $file
    if (Test-Path $path) {
        try {
            Remove-Item -Path $path -Force
            Write-Host "Arquivo $file removido." -ForegroundColor Green
        } catch {
            Write-Host "Erro ao remover arquivo $file: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "Arquivo $file não encontrado." -ForegroundColor Cyan
    }
}

Write-Host "Limpando pastas bin e obj..." -ForegroundColor Yellow
$folders = @("bin", "obj")

foreach ($folder in $folders) {
    $path = Join-Path -Path $PSScriptRoot -ChildPath $folder
    if (Test-Path $path) {
        try {
            Remove-Item -Path $path -Recurse -Force
            Write-Host "Pasta $folder removida." -ForegroundColor Green
        } catch {
            Write-Host "Erro ao remover pasta $folder: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "Pasta $folder não encontrada." -ForegroundColor Cyan
    }
}

Write-Host "Iniciando aplicativo..." -ForegroundColor Yellow
try {
    dotnet run
} catch {
    Write-Host "Erro ao iniciar aplicativo: $_" -ForegroundColor Red
}
