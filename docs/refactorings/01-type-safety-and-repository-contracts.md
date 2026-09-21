# Instruction 01 — Type Safety and Repository Contracts

## Goal

残っている primitive/raw string ベースの識別子・イベント表現を整理し、Application / Infrastructure 境界の型安全性を上げる。

## Changes

- `DeskId` を typed ID 化する。
  - `CurrentUser`
  - `UserSummary`
  - `IBusinessDateResolver`
  - Trader / desk scope を扱う query / repository interface
- Event model の raw ID を typed ID に寄せる。
  - `CaseId`
  - `UserId?`
  - 必要に応じて `QuoteId`
- `"Rfq"` / `"Quote"` のような event kind の raw string 分岐を廃止する。
- `PendingEvent` は RFQ event / Quote event を型で区別できる構造にする。
  - nullable ID と null-forgiving (`!`) による判定を避ける。
- `PersistedEvent` も Application 内で扱う識別子は可能な限り typed ID を使う。
- `IRfqCaseRepository.GetExpiredQuotesAsync` の default implementation を削除し、実装必須にする。
  - test fake / in-memory implementation も明示的に実装する。

## Constraints

- Domain の既存 state model / transition model は変更しない。
- EF entity / DB column は primitive 型のままでよい。
- DB 境界で primitive ⇄ typed ID を変換する。
- API / Web 層は対象外。
- 挙動変更は行わず、型安全性と契約明確化に限定する。

## Done When

- Application 内で desk identity を raw `string` として扱わない。
- event 種別判定に `"Rfq"` / `"Quote"` の文字列比較を使わない。
- `PendingEvent` が不正な ID 組み合わせを表現できない。
- `GetExpiredQuotesAsync` の未実装がコンパイル時に検出される。
- 既存テストが通る。
