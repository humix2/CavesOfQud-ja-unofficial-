# Caves of Qud 日本語化（非公式・個人用）

Caves of Qud（Steam 通常版）を日本語で遊ぶための個人用 MOD とツール一式です。

- **QudJP Lite**：通常版用の軽量日本語化 MOD。現在はこちらを使っています
- **QudJA**：公式のローカライズ基盤（`lang-experimental` ブランチ）用。現在は保留中です（後半に記載）

翻訳データの多くは、ワークショップの日本語化 MOD「QudJP」（MIT License）を流用しています。QudJP Lite はその翻訳データを使いながら、ゲーム内での処理をできるだけ軽くした自作版です。会話・メッセージ・メニューなどは自作訳で差し替えたり、足したりしています。

---

## QudJP Lite（通常版用・軽量日本語化）

### 使い方

1. ワークショップの QudJP（3718988020）は **サブスクライブしたまま**、ゲーム内の Mods 画面で **無効** にしておく（翻訳データの取得元として使います）
2. PowerShell で `./deploy.ps1` を実行する
   - Steam やゲームのインストール先が既定と違う場合は、引数で指定します

     ```powershell
     ./deploy.ps1 -QudJPPath "<workshop>\content\333640\3718988020" -GamePath "<steamapps>\common\Caves of Qud"
     ```

   - 翻訳の生成に Python 3 を使います
3. ゲームを起動し、Mods 画面で「QudJP Lite」を有効にして再起動する

QudJP や自作訳を更新したら、`./deploy.ps1` を再実行してゲームを再起動すると反映されます。

### 日本語化される範囲

| 範囲 | 方法 |
|---|---|
| アイテム・クリーチャー・変異・スキル・書物などのデータ | QudJP の翻訳 XML をそのまま使う（ゲーム本体が読み込むので実行時の負担なし） |
| 会話 | QudJP の訳をベースに、序盤〜中盤の主要な会話を自作訳で差し替え（ベイ・ラー、ジョッパ、グリットゲート、シックスデイ・スティルト、キャクキャ、ハイドロポン ほか約2,300文） |
| メニュー・見出し・ボタン | 完全一致の辞書（QudJP の辞書＋自作の上書き） |
| 変異の説明・食事の効果など、数値入りの文 | 数値を記号に置き換えた「型」で訳す |
| メッセージログ・ポップアップ | 正規表現のパターンで訳す（QudJP のパターン＋自作） |
| Esc メニュー・たき火などの選択肢、Yes/No ボタン | 選択肢を受け取る入口で訳す |
| アビリティバー・アビリティ一覧、タイトル画面、ロード画面 | 個別に対応 |

### 仕組み（QudJP との違い）

| | QudJP（ワークショップ版） | QudJP Lite |
|---|---|---|
| フォント | 11MB の Noto フォントを同梱、4096px アトラス | Windows 標準フォント（BIZ UDゴシック → 游ゴシック → メイリオ → MSゴシック）を起動時に1回登録 |
| データ翻訳 | XML | 同じ XML をそのまま使う |
| UI の固定文字列 | Harmony パッチ約530か所＋表示のたびに正規表現で置換（**重さの主因**） | `UITextSkin` の SetText/Apply の入口で完全一致の辞書引き（[UITranslation.cs](QudJPLite/UITranslation.cs)） |
| 数値入りの UI 文 | 同上 | 数値・ダイス表記を記号に置き換えた「型」で引く。行ごとの結果を覚えておき、2回目以降は辞書引きだけ（[UITranslation.cs](QudJPLite/UITranslation.cs)） |
| メッセージログ・ポップアップ | 同上 | ログに追加される瞬間と、ポップアップを開く入口で1回だけ正規表現で訳す（[MessageTranslation.cs](QudJPLite/MessageTranslation.cs)） |
| UITextSkin を通らない文字 | 同上 | アビリティ名は作成・読み込み時に1回だけ訳す（[AbilityTranslation.cs](QudJPLite/AbilityTranslation.cs)）。コードに埋め込まれた `[on]` `[off]` などは、起動時にコード内の文字列を差し替える（[LiteralTranslation.cs](QudJPLite/LiteralTranslation.cs)、以降の負担なし） |
| ツールチップで日本語が透明になる問題 | 表示のたびにツールチップを作り直して修復 | 日本語を含むテキストだけ、描画前に透明度を親に合わせる（[SubMeshAlphaFix.cs](QudJPLite/SubMeshAlphaFix.cs)） |
| 本の日本語の折り返し | ページ生成時に CJK 用の折り返し | 同じくページ生成時に1回だけ、日本語を含む段落を1行47文字で折り返す（[BookWrap.cs](QudJPLite/BookWrap.cs)） |
| 長い日本語段落の先頭1文字が消える問題 | — | ゲームの折り返し（`TextBlock.ParseSpan`）は、改行直後の単語が1行に収まらないと先頭1文字を捨てるバグがある。空白のない長い日本語を含む文だけ、この折り返しを飛ばして TextMeshPro に任せる |

表示のたびに走る処理は、辞書引きと、覚えておいた結果の再利用だけです。正規表現は、メッセージやポップアップが出る瞬間に1回だけ使います。

### 翻訳の足し方

どれも `./deploy.ps1` の再実行で反映されます。

| ファイル | 中身 | 書き方 |
|---|---|---|
| [tools/ui-overrides.tsv](tools/ui-overrides.tsv) | メニューなどの完全一致の訳。QudJP の辞書より優先 | `英語<TAB>訳` |
| [tools/ui-templates.tsv](tools/ui-templates.tsv)、[tools/ui-templates-mutations.tsv](tools/ui-templates-mutations.tsv) | 数値入りの文の型 | `Cooldown: <#0> rounds<TAB>クールダウン: <#0>ラウンド`（数値は `<#0>` `<#1>` …） |
| [tools/ui-messages.tsv](tools/ui-messages.tsv) | メッセージ・ポップアップの正規表現。QudJP のパターンより優先 | `^You gain (\d+) XP!$<TAB>経験値を{0}獲得した！`（捕捉した部分は `{0}` `{1}` …。辞書にあれば訳される） |
| `tools/conv/*.txt` | 会話の自作訳（[tools/conv_tool.py](tools/conv_tool.py) が配置時に差し替える） | `### 会話ID \| 場所` / `> 英語原文` / 訳。訳のルールと用語集は [docs/translation-style.md](docs/translation-style.md) |
| [tools/terms.tsv](tools/terms.tsv) | 全訳文の一括置換（訳語の統一） | `置換前<TAB>置換後`（例: 六日のスティルト → シックスデイ・スティルト） |

変異の説明の英文を一覧にしたいときは、配置先の `Mods/QudJPLite/` に空の `dump-mutations.flag` を置いてゲームを起動します。`mutation-dump.txt` が出力されるので、[tools/make_templates.py](tools/make_templates.py) で未訳の型を一覧にできます。

### 現在の制約

- **アイテム名・液体名の組み立て部分が英語のまま残る**：ゲームが名前を組み立てるときの形容詞や言い回しが訳されません（例: `small 石`、`水たまり of salty asphalt`）
- **上部ステータスバーの一部（`Quenched`、日付など）と ACTIVE EFFECTS**：未対応です
- **パターンにない文は英語のまま**：英語の文の中に日本語の名前が入ることがあります（例: `The ヒヒ beats at …`）。見つけたら [tools/ui-messages.tsv](tools/ui-messages.tsv) に足します
- **公式のローカライズ基盤**（`lang-experimental` / `StreamingAssets/ExampleLanguage`）は、2026-11 時点ではまだ通常版に入っていません。通常版に入れば、そちらへ移行するのが最も軽く確実です

---

## QudJA（公式ローカライズ基盤版・lang-experimental 用・保留中）

Steam のベータで `lang-experimental` を選んでいる場合に使う版です。Harmony は使いません。

lang-experimental ではランキング（Daily など）の取得・送信がゲーム側で無効になっているため、通常版に戻して保留にしています（2026-10-31 時点、ビルド 2.0.212.32）。

- [QudJA/](QudJA/)：言語登録（[Languages.xml](QudJA/Languages.xml)）と、日本語用の文法クラス（[TranslatorJapanese.cs](QudJA/TranslatorJapanese.cs)：冠詞なし・日本語の改行・列挙・数詞）
- [tools/build_ja.py](tools/build_ja.py)：ゲームの `ExampleLanguage`（翻訳対象の一覧）と QudJP の訳を突き合わせ、`Lang="ja"` の翻訳ファイルを生成する
- `tools/translate/batch_*.tsv`：自作の翻訳（src=英語原文 / ja=訳）。2,835件
- `tools/overrides/*.xml`：上を `<strings>` XML にしたもの。build_ja.py が最優先で使う

使い方: `./deploy_ja.ps1` → ゲームの Mods 画面で QudJA を有効化（QudJP Lite と QudJP は無効）→ メインメニューの言語選択で「日本語」。

再開手順: Steam で lang-experimental に切り替え → `./deploy_ja.ps1`。新しいビルドの ExampleLanguage で再生成されます。ID が変わった訳は未訳一覧に戻るので、`tools/make_batches.py` で束を作り直します。

**注意**: QudJA は通常版では使えません。文法クラスがコンパイルエラーになり、`Lang="ja"` の XML も無条件に読み込まれるためです。通常版では Mods フォルダに置かないでください。

手元にだけ置いているもの（ゲーム本体のデータを含むため、リポジトリには入れていません）:

| 場所 | 内容 |
|---|---|
| `reference/lang-experimental-2.0.212.32/` | 公式の見本（ExampleLanguage）と会話の英語原文のコピー。ゲームが通常版でも変換スクリプトが動く |
| `snapshots/QudJA-2.0.212.32/` | 生成済みの MOD 一式とカバー率レポート |

---

## ライセンス・クレジット

- 翻訳データの多くは QudJP（Steam ワークショップ 3718988020、MIT License、© ToaruPen & Contributors）のものです。配置時に `LICENSE-QudJP.txt` を同梱します
- Caves of Qud は Freehold Games の作品です。このリポジトリは非公式の個人プロジェクトで、Freehold Games とは関係ありません
