# CaseIdの増分整数化

## 変更理由

RFQの案件番号として人が読み取りやすい値を使うため、`CaseId`をUUIDから増分整数へ変更する。

現時点では表示専用の案件番号を別に設けず、次の値を同一とする。

```text
人間向け案件番号 = 内部CaseId = 増分整数
```

## 変更内容

- C#の`CaseId`を`Guid`から正の`long`へ変更
- PostgreSQLの`case_id`を`uuid`から`bigint`へ変更
- PostgreSQL sequence `rfq_case_id_seq`を採番元として追加
- APIの`caseId`を整数へ変更
- フロントエンドの`caseId`を`number`へ変更
- 既存のCase、CaseCurrent、Revision間の参照関係を整数CaseIdで維持

## 採番ルール

- PostgreSQL sequenceによって一意な増分値を採番する
- 同時作成でも同じCaseIdを発行しない
- 登録失敗、ロールバック、削除などによる欠番を許容する
- CaseIdの再利用は行わない
- ClientやSecurityなどの入力検証に失敗した場合は採番しない

## Migration

既存のUUID CaseIdには、作成日時と旧CaseIdの順序を基準として新しい整数CaseIdを割り当てる。

`rfq_cases`、`case_currents`、`rfq_revisions`の参照は同じ番号へ変換し、CaseとCurrent Revisionの関係を維持する。

## 今回採用しないもの

- `RFQ-2026-000001`のような表示専用番号
- 年度単位やDesk単位での採番
- 欠番を埋める仕組み

表示専用番号が必要になった場合は、内部CaseIdとは別の項目として後続変更で検討する。
