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

EOD は historical snapshot ではなく、**current desk-wide remaining-work view** とする。

- current user's desk の RFQ のみを対象にする。
- `CreatedAt` が対象日であることを条件にしない。
- 現在の状態を使って `Open / Hit / Away` を集計する。
- `Open` は current lifecycle/status 上で未完了の RFQ（Active / Presented）を対象とする。
- `Hit` / `Away` は current state がそれぞれ Hit / Away の RFQ を対象とする。
- historical EOD snapshot の再構築や event replay は行わない。

## Tests

- JST 00:00 境界の前後で Past RFQ の検索結果が正しいこと。
- UTC 日付と desk local date が異なるケースを確認すること。
- `To` 指定日全体が含まれ、翌日 00:00 は含まれないこと。
- EOD に他 desk の RFQ が混ざらないこと。
- 前日以前に作成された未完了 RFQ も EOD の `Open` に含まれること。
- current state が Hit / Away の RFQ が正しく集計されること。

## Constraints

- Domain state model は変更しない。
- EOD を event sourcing / historical snapshot 化しない。
- API / Web 層は対象外。
- DB timestamp の保存形式は UTC のまま維持する。

## Done When

- Past RFQ の日付検索に UTC 00:00 固定解釈が残っていない。
- EOD に `CreatedAt == 対象日` 相当の制約が残っていない。
- EOD が current user's desk scope で集計される。
- timezone 境界と EOD scope のテストが追加され、既存テストも通る。
