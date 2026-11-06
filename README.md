# QudJA（公式ローカライズ基盤版・lang-experimental 用）

Steam のベータで `lang-experimental` を選んでいる場合はこちら。Harmony 不使用。

- [QudJA/](QudJA/)：言語登録（[Languages.xml](QudJA/Languages.xml)）と日本語用の文法クラス（[TranslatorJapanese.cs](QudJA/TranslatorJapanese.cs)：冠詞なし・日本語の改行・列挙・数詞）
- [tools/build_ja.py](tools/build_ja.py)：ゲームの `ExampleLanguage`（翻訳対象の一覧）と QudJP の訳を突き合わせ、`Lang="ja"` の翻訳ファイルを生成する
- `tools/overrides/*.xml`：手作業で足す訳（`<strings><string Context=".." ID="英語">訳</string></strings>`）。最優先で使われる
- `dist/report/`：カバー率（coverage.tsv）、未訳一覧（untranslated_strings.tsv）、壊れた変数で不採用にした訳（rejected_broken_vars.tsv）

使い方: `./deploy_ja.ps1` → ゲームの Mods 画面で QudJA を有効化（QudJP Lite と QudJP は無効）→ メインメニューの言語選択で「日本語」。

### 保存している記録（2026-10-31 時点、ビルド 2.0.212.32）

lang-experimental ではランキング（Daily 等）の取得・送信がゲーム側で無効化されているため、一旦通常版に戻した。再開用に以下を残している。

| 場所 | 内容 |
|---|---|
| `tools/translate/batch_*.tsv` | 自作の翻訳（src=英語原文 / ja=訳）。2,835件 |
| `tools/overrides/*.xml` | 上を `<strings>` XML にしたもの（build_ja.py が最優先で使う） |
| `reference/lang-experimental-2.0.212.32/` | 公式の見本（ExampleLanguage）と会話の英語原文のコピー。ゲームが通常版でも変換スクリプトが動く |
| `snapshots/QudJA-2.0.212.32/` | 生成済みの MOD 一式とカバー率レポート。lang-experimental でそのまま Mods にコピーして使える |

**注意**: QudJA は通常版では使えない（文法クラスがコンパイルエラーになり、`Lang="ja"` の XML も無条件に読み込まれる）。通常版では Mods フォルダに置かないこと。

再開手順: Steam で lang-experimental に切り替え → `./deploy_ja.ps1`（新しいビルドの ExampleLanguage で再生成される。ID が変わった訳は未訳一覧に戻るので `tools/make_batches.py` で束を作り直す）。

---

# QudJP Lite（Caves of Qud 軽量日本語化・通常版用）

ワークショップの日本語化MOD「QudJP」が重いため、負荷の少ない部分だけを取り出した自作版。

## 仕組み

| | QudJP（ワークショップ版） | QudJP Lite（このMOD） |
|---|---|---|
| フォント | 11MB の Noto フォントを同梱、4096px アトラス | Windows 標準フォント（BIZ UDゴシック → 游ゴシック → メイリオ → MSゴシック）を起動時に1回登録 |
| データ翻訳（アイテム・クリーチャー・会話・変異・スキル・書物など） | XML | 同じ XML をそのまま流用（ゲーム本体が読み込むので実行時コストなし） |
| ツールチップで日本語が透明になる問題 | 表示のたびにツールチップを作り直して修復 | 日本語を含むテキストだけ、描画前に透明度を親に合わせる（[SubMeshAlphaFix.cs](QudJPLite/SubMeshAlphaFix.cs)） |
| UI の固定文字列（メニュー・見出し・ボタン等） | Harmony パッチ約530か所＋正規表現で毎回置換（**重さの主因**） | `UITextSkin` の SetText/Apply 入口だけで完全一致の辞書引き（[UITranslation.cs](QudJPLite/UITranslation.cs)、辞書は [tools/build_lite_dict.py](tools/build_lite_dict.py) が QudJP の辞書と自作訳から生成） |
| 本の日本語の折り返し | ページ生成時に CJK 用の折り返し | 同じくページ生成時に1回だけ、日本語を含む段落を1行47文字で折り返す（[BookWrap.cs](QudJPLite/BookWrap.cs)）。ゲームは空白でしか折り返さないため、ないと段落が1行になって画面からはみ出す |
| 長い日本語段落の先頭1文字が消える問題 | — | ゲームの折り返し（`TextBlock.ParseSpan`）は、改行直後の単語が1行に収まらないと先頭1文字を捨てて空行を足すバグがある。72字以上空白のない日本語を含む文だけ、この折り返しを飛ばして TextMeshPro に任せる（[UITranslation.cs](QudJPLite/UITranslation.cs)） |
| 戦闘メッセージ・自動生成文（名前や数が埋め込まれる文） | 同上 | なし（英語のまま） |
| 会話の訳し直し | — | `tools/conv/*.txt` の自作訳で、配置時に QudJP の会話ファイルの文を差し替える（[tools/conv_tool.py](tools/conv_tool.py)）。英語原文と照合し、原文が変わった所は QudJP の訳のまま。QudJP に無い場面は英語原文と同じ位置に要素を足す。訳のルールは [docs/translation-style.md](docs/translation-style.md) |
| 訳語の統一 | — | 配置時に [tools/terms.tsv](tools/terms.tsv) の訳語を全訳文ファイルで一括置換（例: 六日のスティルト → シックスデイ・スティルト） |

## 使い方

1. ワークショップの QudJP は **サブスクライブしたまま**、ゲーム内の Mods 画面で **無効** にしておく（翻訳データの取得元として使う）
2. PowerShell で `./deploy.ps1` を実行
3. ゲームを起動し、Mods 画面で「QudJP Lite」を有効化して再起動

QudJP が更新されたら `./deploy.ps1` を再実行すれば翻訳も更新される。

## 制約

- メニュー・ボタン・戦闘ログなど、ゲームの C# コード内に直接書かれた英語は翻訳されない
- 英語の文章に日本語の名前が入るため、`You hit the 熊.` のように冠詞が残る
- ニュースで告知された公式ローカライズ基盤（`lang-experimental` ブランチ / `StreamingAssets/ExampleLanguage`）は、2026-10 時点の通常ブランチ（2.0.211）にはまだ入っていない。正式版に入れば、そちらに移行するのが最も軽く確実

## ライセンス

翻訳データは QudJP（Steam ワークショップ 3718988020、MIT License, © ToaruPen & Contributors) のもの。配置時に `LICENSE-QudJP.txt` を同梱する。
