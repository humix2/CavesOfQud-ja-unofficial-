# QudJP Lite をゲームのローカル Mods フォルダへ配置する。
# 翻訳 XML はワークショップの QudJP (MIT License) から毎回コピーするので、
# QudJP が更新されたら、このスクリプトを再実行するだけで翻訳も最新になる。
param(
    [string]$QudJPPath = "D:\Program Files (x86)\Steam\steamapps\workshop\content\333640\3718988020",
    [string]$ModsPath  = "$env:USERPROFILE\AppData\LocalLow\Freehold Games\CavesOfQud\Mods",
    [string]$GamePath  = "D:\Program Files (x86)\Steam\steamapps\common\Caves of Qud",
    [switch]$SkipCompileCheck
)
$ErrorActionPreference = 'Stop'

$src  = Join-Path $PSScriptRoot 'QudJPLite'
$dest = Join-Path $ModsPath 'QudJPLite'

if (-not (Test-Path (Join-Path $QudJPPath 'Localization'))) {
    throw "QudJP の翻訳データが見つかりません: $QudJPPath`nワークショップで QudJP をサブスクライブしたままにしてください（ゲーム内では無効で OK）。"
}

# 配置前のコンパイル確認。エラーがあれば配置しない（壊れた MOD を Mods フォルダに入れないため）
$managed = Join-Path $GamePath 'CoQ_Data\Managed'
$checkProj = Join-Path $PSScriptRoot 'tools\compile-check\QudJPLite.Check.csproj'
if ($SkipCompileCheck) {
    Write-Warning "コンパイル確認を省略しました (-SkipCompileCheck)"
} elseif (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Warning "dotnet が見つからないため、コンパイル確認を省略しました（.NET SDK を入れると配置前に確認できます）"
} elseif (-not (Test-Path $managed)) {
    throw "ゲームの DLL が見つかりません: $managed`n-GamePath でゲームのインストール先を指定してください。"
} else {
    $out = & dotnet build $checkProj -v q -nologo "-p:GameManaged=$managed" 2>&1
    if ($LASTEXITCODE -ne 0) {
        $out | Select-String 'error' | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
        throw "コンパイルエラーがあるため配置を中止しました（今の配置はそのまま残っています）。"
    }
    Write-Host "コンパイル確認: OK"
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

# メッセージログ用の正規表現パターン（追加の瞬間に1回だけ訳す）
python -I (Join-Path $PSScriptRoot 'tools\build_message_patterns.py') $QudJPPath (Join-Path $locDest 'ui-messages.tsv')
if ($LASTEXITCODE -ne 0) { throw "メッセージのパターンの生成に失敗しました" }

# 数値入りの UI 文の「型」（変異の説明など）
$tpl = Get-ChildItem (Join-Path $PSScriptRoot 'tools') -Filter 'ui-templates*.tsv' | Sort-Object Name | ForEach-Object { [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8) }
[IO.File]::WriteAllText((Join-Path $locDest 'ui-templates.tsv'), ($tpl -join "`n"), (New-Object Text.UTF8Encoding $false))

# 会話の自作訳（tools/conv/*.txt）で QudJP の会話の文を差し替える。
# 英語原文と照合するので、ゲーム更新で原文が変わった所は QudJP の訳のまま残る
$baseConv = Join-Path $GamePath 'CoQ_Data\StreamingAssets\Base\Conversations.xml'
python -I (Join-Path $PSScriptRoot 'tools\conv_tool.py') apply $baseConv (Join-Path $locDest 'Conversations.jp.xml') (Join-Path $PSScriptRoot 'tools\conv')
if ($LASTEXITCODE -ne 0) { throw "会話の自作訳の適用に失敗しました" }

# 訳語の一括置換（tools/terms.tsv）。QudJP の訳と自作訳で呼び名をそろえる
python -I (Join-Path $PSScriptRoot 'tools\term_replace.py') (Join-Path $PSScriptRoot 'tools\terms.tsv') $locDest
if ($LASTEXITCODE -ne 0) { throw "訳語の一括置換に失敗しました" }

# ライセンス表記（QudJP は MIT License）
Copy-Item (Join-Path $QudJPPath 'LICENSE') (Join-Path $dest 'LICENSE-QudJP.txt')

$ver = (Get-Content (Join-Path $QudJPPath 'manifest.json') -Raw | ConvertFrom-Json).Version
Write-Host "配置完了: $dest"
Write-Host "  翻訳 XML: $($xmls.Count) ファイル (QudJP $ver より)"
Write-Host "ゲームの Mods 画面で 'QudJP Lite' を有効にし、'Caves of Qud Japanese Mod' は無効のままにしてください。"
