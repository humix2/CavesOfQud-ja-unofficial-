# QudJP Lite をゲームのローカル Mods フォルダへ配置する。
# 翻訳 XML はワークショップの QudJP (MIT License) から毎回コピーするので、
# QudJP が更新されたら、このスクリプトを再実行するだけで翻訳も最新になる。
param(
    [string]$QudJPPath = "D:\Program Files (x86)\Steam\steamapps\workshop\content\333640\3718988020",
    [string]$ModsPath  = "$env:USERPROFILE\AppData\LocalLow\Freehold Games\CavesOfQud\Mods"
)
$ErrorActionPreference = 'Stop'

$src  = Join-Path $PSScriptRoot 'QudJPLite'
$dest = Join-Path $ModsPath 'QudJPLite'

if (-not (Test-Path (Join-Path $QudJPPath 'Localization'))) {
    throw "QudJP の翻訳データが見つかりません: $QudJPPath`nワークショップで QudJP をサブスクライブしたままにしてください（ゲーム内では無効で OK）。"
}

# 古い配置を消してから作り直す（このフォルダは本スクリプト専用）
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force $dest | Out-Null

# MOD 本体（スクリプトと manifest）
Copy-Item (Join-Path $src '*') $dest -Recurse

# 翻訳 XML（ゲーム本体がネイティブに読み込む部分だけ）。JSON 辞書は QudJP.dll 専用なので不要
$locSrc  = Join-Path $QudJPPath 'Localization'
$locDest = Join-Path $dest 'Localization'
$xmls = Get-ChildItem $locSrc -Recurse -Filter *.xml
foreach ($f in $xmls) {
    $rel = $f.FullName.Substring($locSrc.Length + 1)
    $to  = Join-Path $locDest $rel
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    Copy-Item $f.FullName $to
}

# UI 辞書（完全一致の英→日表。QudJP の辞書 + tools/overrides の自作訳）
$env:PYTHONIOENCODING = 'utf-8'
New-Item -ItemType Directory -Force $locDest | Out-Null
python -I (Join-Path $PSScriptRoot 'tools\build_lite_dict.py') $QudJPPath (Join-Path $locDest 'ui-dictionary.tsv')
if ($LASTEXITCODE -ne 0) { throw "UI辞書の生成に失敗しました" }

# ライセンス表記（QudJP は MIT License）
Copy-Item (Join-Path $QudJPPath 'LICENSE') (Join-Path $dest 'LICENSE-QudJP.txt')

$ver = (Get-Content (Join-Path $QudJPPath 'manifest.json') -Raw | ConvertFrom-Json).Version
Write-Host "配置完了: $dest"
Write-Host "  翻訳 XML: $($xmls.Count) ファイル (QudJP $ver より)"
Write-Host "ゲームの Mods 画面で 'QudJP Lite' を有効にし、'Caves of Qud Japanese Mod' は無効のままにしてください。"
