# 公式ローカライズ基盤 (lang-experimental) 向けの日本語パック QudJA を生成し、ゲームの Mods フォルダへ配置する。
# ゲームや QudJP が更新されたら、このスクリプトを再実行すれば訳が再生成される。
param(
    [string]$ModsPath = "$env:USERPROFILE\AppData\LocalLow\Freehold Games\CavesOfQud\Mods"
)
$ErrorActionPreference = 'Stop'
$env:PYTHONIOENCODING = 'utf-8'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

python -I (Join-Path $PSScriptRoot 'tools\build_ja.py')
if ($LASTEXITCODE -ne 0) { throw "生成に失敗しました" }

$src  = Join-Path $PSScriptRoot 'dist\QudJA'
$dest = Join-Path $ModsPath 'QudJA'
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item $src $dest -Recurse

Write-Host ""
Write-Host "配置完了: $dest"
Write-Host "ゲームで:"
Write-Host "  1. Mods 画面で 'QudJA' を有効、'QudJP Lite' と 'Caves of Qud Japanese Mod' は無効にする"
Write-Host "  2. 再起動後、メインメニューの言語選択で「日本語」を選ぶ"
