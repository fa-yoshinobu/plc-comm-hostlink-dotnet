# refactor-instructions.md

plc-comm-hostlink-dotnet のリファクタリング指示書。
この文書は実装担当モデル向けの完結した作業指示である。実装前にこの文書全体を読むこと。

> **最重要の前提**: このライブラリは NuGet に公開済み(`PlcComm.KvHostLink` 0.1.11)で
> あり、Host Link ASCII フレームは実機 KV-5000 での restore-safe 検証記録(`TODO.md`)に
> 紐づく。**公開 API と送信フレームの文字列を 1 文字たりとも変えてはならない。**
>
> このリポジトリは plc-comm 一族の **Host Link 基準実装**であり、一族の中で最も健全な
> 部類である(クライアント 522 行、CI に docs カバレッジ検査・サンプル一覧検査まで組込済、
> フレームベクトルテストあり)。Rust 版(plc-comm-hostlink-rust)の指示書と同じく、
> 本タスクの中心は**構造変更ではなく golden フレームベクトルの網羅拡充**である。
> 変更すべきものが見つからなければ、それを正直に報告して終了してよい。
> 無理に変更量を増やすことを最も強く禁ずる。

---

## Objective

公開 API・送信フレーム文字列・クロススタック互換を一切壊さずに:

1. **`HostLinkFrameVectorTests`(157 行)のコマンド網羅を広げる**
   (未収録コマンドの現在の送信フレームを特性テストとして固定する)
2. (任意・小)`KvHostLinkClientExtensions.cs`(1,069 行)内の read-plan 機構
   (private 型群、23〜92 行)の internal クラス分離

---

## Project Understanding

### 何のライブラリか

KEYENCE KV シリーズと上位リンク(Host Link、TCP/UDP、ASCII コマンド)で通信する
.NET 9 ライブラリ。Rust / Python / Node-RED 版の基準実装。
`OpenAndConnectAsync` → `QueuedKvHostLinkClient` が推奨入口。

### モジュール構成(src/PlcComm.KvHostLink/、計約 2,700 行)

| ファイル | 行数 | 内容 |
|---|---|---|
| `KvHostLinkClientExtensions.cs` | 1,069 | 契約ヘルパ(ReadTyped / ReadNamed / Poll / single-request / chunked / TimerCounter)+ read-plan 最適化(private、23〜92 行に型群) |
| `KvHostLinkClient.cs` | 522 | クライアント本体(コマンド面 + トランスポート) |
| `KvHostLinkDeviceRanges.cs` | 463 | モデル別レンジカタログ |
| `QueuedKvHostLinkClient.cs` | 268 | 直列化ラッパ |
| `KvHostLinkDevice.cs` / `KvHostLinkAddress.cs` | 392 | アドレス解析・検証 |

### テスト / CI

- `tests/`: `KvHostLinkClientExtensionsTests`(379)/ `HostLinkFrameVectorTests`(157)/
  `KvHostLinkDeviceTests` ほか
- `run_ci.bat`(7 ステップ): build(lib / tests)→ test → format →
  samples 3 種 build → `scripts/check_high_level_docs.ps1` →
  `scripts/check_sample_inventory.ps1`

---

## Behaviors To Preserve(絶対に壊さない既存挙動)

1. **公開 API**: すべての public 型・メソッド・シグネチャ・既定値。
2. **送信フレームの文字列**: 既存フレームベクトルが契約。既存ベクトルの編集禁止
   (追加は Phase 1 の手順でのみ可)。
3. **プロトコル固定事項**(TODO.md / Rust 版指示書と共通): `AT` の送信前書込拒否、
   `T`/`C` プリセット書込の機種制限、`read_typed("T10","D")` のプリセット互換挙動、
   コメント読取の XYM エイリアス、Shift-JIS デコード。
4. **read-plan の分割規則と結果順序**(`ReadNamedAsync`)。
5. **`QueuedKvHostLinkClient` の直列化セマンティクス**(plc-scope-dotnet が利用)。
6. **NuGet パッケージ ID・バージョン(0.1.11)・CHANGELOG**: 変更しない。

---

## Non-Negotiables(交渉不可の制約)

- 最初に `git status` を確認する。未コミット変更があれば混ぜず、報告して停止する。
- 編集前に Baseline Commands をすべて実行し、結果(テスト件数含む)を記録する。
- 変更は小さく戻しやすい単位。コミットはユーザーの指示があるまで行わない。
- 無関係な整形・「ついで」リファクタリングをしない。
- NuGet 依存を追加しない。csproj / props を変更しない。
- Phase 1 で追加する golden 値は「実装の現在の出力」を機械的に採取したものに限る。
  マニュアルから起こした期待値を勝手に正として書かない(食い違いは Stop And Ask)。
- 既存テストの既存アサーションを変更しない(追加のみ可)。
- 実機 PLC への接続を行わない。
- 正しさが不明な場合は実装を止め、「Stop And Ask」として質問を報告書に書く。

---

## Stop And Ask Conditions(即時停止して質問する条件)

- ベクトル採取中に、実装の送信フレームが README / Rust 版ベクトル / KEYENCE マニュアルの
  記述と食い違って見えた(**修正せず**質問として残す)
- 既存テストが自分の変更後に落ちた ⇒ 即座に巻き戻して報告
- D2 の移動対象が予想に反してインスタンス状態に依存していた ⇒ スキップして報告
- 公開 API・フレーム文字列・エラー文言に影響しうる変更が必要に見えた
- 本書の Debt Map に無い大きな問題を発見した(報告のみ)

---

## Baseline Commands

作業ディレクトリ: リポジトリルート。.NET 9 SDK。Windows 推奨(`run_ci.bat` の
PowerShell 検査スクリプト)。実機 PLC 不要・接続禁止。

```powershell
git status
dotnet build src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj
dotnet build tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj
dotnet test tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj --no-build
dotnet format PlcComm.KvHostLink.sln --verify-no-changes
powershell -ExecutionPolicy Bypass -File scripts\check_high_level_docs.ps1
powershell -ExecutionPolicy Bypass -File scripts\check_sample_inventory.ps1
```

(= `run_ci.bat` 相当。可能なら `run_ci.bat` をそのまま実行してよい)

---

## Debt Map

行番号は調査時点(main, commit `4e49681`)のアンカー。ドリフトしていたら宣言名で探すこと。

### D1. フレームベクトルのコマンド網羅不足 【実装可 / 主作業】

- **根拠**: `HostLinkFrameVectorTests.cs` は 157 行で、`KvHostLinkClient` の公開コマンド面
  に対して一部しか固定していない。Rust 版では同じ構図(20 ベクトル / 12 コマンド種)が
  主負債として指示済みであり、基準実装である本リポジトリ側の網羅はそれ以上に重要。
- **改善案**: 既存テストの方式に従い、未収録コマンド(forced set/reset、連続書込の
  レガシー形、拡張ユニットバッファ、モニタ読出側、時刻設定、モデル照会、コメント読取の
  送信側等 — 実際の一覧は `KvHostLinkClient` の公開面と突き合わせて作成)の現在の
  送信フレームを採取してベクトル追加する。
- **検証**: 追加後に全テストが通ること。追加一覧を報告書に記載。
- **リスク**: 低(テスト追加のみ)。

### D2. Extensions 内の read-plan 機構の同居 【任意・小】

- **根拠**: `ReadPlanValueKind` / `ReadPlanSegment` / `CompiledReadNamedPlan` 等の
  private 型群(23〜92 行)と plan コンパイラが契約ヘルパと同じ 1,069 行のファイルに同居。
  Rust 版 helpers.rs の D2 と同型。
- **改善案**: internal クラス(新ファイル)へ move-only 分離。D1 完了後、時間や確信が
  足りなければ実施せず提案として報告するだけでよい。
- **検証**: `KvHostLinkClientExtensionsTests` が無修正で通ること。

### D3. その他(現状維持 / 報告のみ)

- `KvHostLinkClient.cs`(522 行)は適正規模。トランスポート分離は不要。
- `KvHostLinkDeviceRanges.cs` はデータ主体。触らない。
- CI(run_ci.bat 7 ステップ + workflows)は完備。変更不要。

---

## Implementation Phases

### Phase 0: 現状確認

1. `git status` 確認(クリーンでなければ停止・報告)
2. Baseline Commands を実行し、結果を記録

### Phase 1: フレームベクトル拡充(D1)

1. `KvHostLinkClient` の公開コマンド一覧 × 既存ベクトルの突き合わせ表を作る
2. 1 コマンドずつ採取 → ベクトル追加 → テスト実行
3. 食い違いを見つけたら、そのコマンドは保留して Stop And Ask に記録し、他は続行

### Phase 2: read-plan 分離(D2、任意)

実施しない場合は提案として報告。

### Phase 3: 検証と報告

全 Verification Requirements を最終実行し、Reporting Format に従って報告。

---

## Verification Requirements

各フェーズ完了時に最低限(Baseline Commands と同一セット)。

最終フェーズでは追加で:

- テスト件数が baseline から増えていること(D1 追加分)
- `git diff` で確認: 既存ベクトル無変更、公開シグネチャ無変更、csproj / props /
  `CHANGELOG.md` / `samples/` 無変更

---

## Reporting Format

1. **Baseline 結果**: 実行コマンドと結果(テスト件数)
2. **D1 の網羅表**: 公開コマンド × ベクトル有無(作業前 / 作業後)
3. **追加したベクトル**: コマンドごとの採取フレーム文字列
4. **文書との食い違い**: 見つけた場合は併記(修正はしない)
5. **D2 の実施有無**: 実施時は移動宣言一覧、見送り時は理由
6. **各フェーズの検証結果**: 最後に実行したコマンドと結果(失敗を隠さない)
7. **Stop And Ask**: 発生した質問と停止範囲
8. **未実施事項**

---

## Out-of-scope Items(やらないこと)

- 公開 API の変更・追加・整理
- 送信フレーム文字列・エラー文言・Shift-JIS 処理の変更(食い違いは報告のみ)
- クライアント/Extensions のコマンド面の再構成
- バージョン変更、`CHANGELOG.md` 更新、NuGet publish
- 依存追加、csproj / props / CI 変更
- `samples/` / `docsrc/` / `internal_docs/` / `scripts/` の変更
- 実機 PLC を使う検証
- 兄弟リポジトリの変更
