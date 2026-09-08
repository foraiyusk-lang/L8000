---
name: night-shift
description: 「残り8000回」を無人で作り進めるための30分ごとの自己レビュー手順。作業の完了確認 → 評価 → 次に必要なものの立案 → 実装、を繰り返す。ユーザーが不在(就寝中)のセッションで使う。
---

# night-shift — 無人開発の自己レビュー

「残り8000回」(`C:\Users\ysk72\Desktop\L8000_new`)をユーザー不在で作り進める。
このスキルが呼ばれたら、以下を順に行う。**毎回、最初に前回の未完了作業を片付けてから**次へ進む。

## 手順

1. **作業の棚卸し**
   - `git log --oneline -15` と `git status` で直近の状態を確認。
   - `NIGHT_SHIFT.md`(リポジトリ直下、無ければ作る)の TODO / 進捗メモを読む。
   - 直前のイテレーションで「途中」だったものがあれば、**それを最優先で完了させる**。

2. **評価(直前の実装へのフィードバック)**
   - 直近で実装した機能を Unity MCP で実際に Play して検証(`manage_editor play` → `execute_code` で状態確認 → `manage_camera screenshot` で目視)。
   - スクリーンショットは必ず自分の目で見て、視認性・世界観・不自然さを評価する。
   - 問題があれば「何が」「なぜ」「どう直すか」を `NIGHT_SHIFT.md` に書き、修正する。

3. **次に必要なものの立案**
   - 残タスク(`NIGHT_SHIFT.md` の計画)から次の1〜2項目を選ぶ。
   - 各項目について **必要性・妥当性・世界観との整合・パフォーマンス影響** を一段落で自問してから着手。
   - 世界観(静かで寂しい、暗い、テキストベース、巻き戻し/エンドロールを登る)を壊すものは実装しない。
   - このPCでカクつくもの(重い毎フレーム処理、大量オブジェクト)は避ける。

4. **実装 → コミット**
   - 小さく実装し、Unity MCP で検証し、`git commit`(日本語メッセージ、Co-Authored-By 付き)。
   - `NIGHT_SHIFT.md` の進捗を更新。

5. **記録**
   - 今回やったこと・次回やること・詰まっている点を `NIGHT_SHIFT.md` に3〜5行で残す。

## トークン枯渇時

- トークンが尽きたら**そこで停止**してよい。`NIGHT_SHIFT.md` に「どこまでやったか」を必ず残す。
- 次回このスキルが呼ばれたら、`NIGHT_SHIFT.md` の続きから再開する。
- 30分ごとの起動はセッションの cron が担う。cron が消えていたらユーザーに再設定を促す。

## 検証の型(Unity MCP)

```
manage_editor play → execute_code(Application.runInBackground=true; 状態を返す)
→ manage_camera screenshot(include_image=true)で目視 → manage_editor stop
```
- `execute_code` はメソッド本体。`using` 不可、`L8000.` で完全修飾。`Thread.Sleep` は時間を止めるので使わない。
- static デバッグフラグ(`PlayerController.DebugAutoClimb` 等)で自動操作。
