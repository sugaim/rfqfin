# Instruction 02 — Business Date and EOD Correctness

## Goal

Past RFQ 検索と EOD 集計を、desk timezone / business-date 前提に合わせて修正する。

## Past RFQ

- `DateOnly` を UTC 00:00 として扱わない。
- `From` / `To` は current user's desk timezone の calendar day として解釈する。
- DB の UTC timestamp に対する検索条件は、desk timezone の日付境界を UTC に変換した半開区間 `[from, to)` とする。
- `To` は指定日の翌日 00:00 を上限とする。
- timezone は desk master から解決する。
- `IBusinessDateResolver` を DateOnly → UTC range 変換へ無理に流用しない。必要なら Infrastructure 内に明示的な変換処理を置く。

## EOD

EOD は historical snapshot ではなく、**current open state と当日の close event の集計**とする。

- current user's desk の RFQ のみを対象にする。
- `Open` は、作成日を問わず、current lifecycle/status 上で未完了の RFQ（Active / Presented）を対象とする。
- `Hit` / `Away` は、指定された `date` の desk-local calendar day 内に発生した `ClosedHit` / `ClosedAway` event を対象とする。
- event の `OccurredAt` に、desk timezone の `[00:00, 翌日 00:00)` を UTC に変換した範囲を適用する。
- historical EOD snapshot の再構築や event replay は行わない。Open は current state、Hit/Away は当日の close event 件数とする。
- `OutcomeCorrected` の扱いは変更しない。
- 新しい `ClosedAt` column は追加しない。

## Tests

- JST 00:00 境界の前後で Past RFQ の検索結果が正しいこと。
- UTC 日付と desk local date が異なるケースを確認すること。
- `To` 指定日全体が含まれ、翌日 00:00 は含まれないこと。
- EOD に他 desk の RFQ が混ざらないこと。
- 前日以前に作成された未完了 RFQ も EOD の `Open` に含まれること。
- 指定日の `ClosedHit` / `ClosedAway` event のみが Hit / Away に集計されること。
- 前日以前の close event と翌日 00:00 の close event が集計されないこと。
- JST の日付境界が正しいこと。
- 他 desk の close event が集計されないこと。

## Constraints

- Domain state model は変更しない。
- EOD を event sourcing / historical snapshot 化しない。
- API / Web 層は対象外。
- DB timestamp の保存形式は UTC のまま維持する。

## Done When

- Past RFQ の日付検索に UTC 00:00 固定解釈が残っていない。
- EOD に `CreatedAt == 対象日` 相当の制約が残っていない。
- EOD が current user's desk scope で集計される。
- EOD の Hit / Away が指定日の desk-local 範囲にある close event から集計される。
- timezone 境界と EOD scope のテストが追加され、既存テストも通る。
