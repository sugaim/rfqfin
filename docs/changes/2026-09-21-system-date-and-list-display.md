# システム日付と一覧表示名称の変更

## 変更理由

受渡日を中心に業務を行うため、Trade Dateをユーザーが入力する項目にはせず、アプリケーション全体で共有するシステム日付を「今日」として扱う。

また、Active RFQ一覧では内部IDだけでなく、ClientとSecurityを人間が識別できる名称を表示する。

## システム日付

- `system_dates`テーブルにシステム日付を保持する。
- 現在の単一キーは`business-today`とする。
- 開発seedの初期値は`2026-09-21`とする。
- `ISystemDateProvider`を通してシステム日付を取得する。
- `ResolveRfqDefaults(SecurityId)`はシステム日付を基準に標準受渡日を計算する。
- Draft作成リクエストとSales入力フォームからTrade Dateを削除する。
- Sales画面右上にシステム日付を状態として表示する。

システム日付はローカルサーバー時刻やUTC日付から都度計算しない。テーブルの値を業務上の「今日」とする。

## Active RFQ一覧

Clientについて次を表示する。

- Client ID
- Client日本語名称

Securityについて次を表示する。

- Security ID
- Security日本語名称
- BBG形式の表示名

既存RFQがマスタに存在しないIDを参照している場合も一覧から除外せず、名称列にはIDをフォールバック表示する。

## API差分

- `GET /api/system-date`を追加する。
- `GET /api/rfq-defaults`から`tradeDate`クエリを削除する。
- Draft作成リクエストから`tradeDate`を削除する。
- Active RFQレスポンスにClient名称、Security日本語名称、BBG表示名を追加する。

## 今回変更しないもの

- システム日付の更新画面および更新API
- 祝日を考慮した本番向け営業日カレンダー
- Confirm以降のRevision／Quote処理
