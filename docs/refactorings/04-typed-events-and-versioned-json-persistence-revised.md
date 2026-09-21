# Instruction 04 — Typed Events and Versioned JSON Persistence

## Goal

Application / Domain と persistence JSON の境界を整理する。

04 の主対象は以下の4点とする。

1. cross-session observable event を Application では typed semantic event として扱う
2. event persistence type code を stable explicit contract にする
3. WorkingQuote / ConfirmedQuote の business JSON を versioned persistence DTO 経由にする
4. persistence serializer contract を Infrastructure に一元化する

03追加対応で GridConfig は `IGridConfigStore` / `EfCoreGridConfigStore` に分離済みであり、
opaque string として扱う構造もできているため、04ではその設計を維持する。

---

## 1. Typed Application Events

### Intent

現在の `PersistedEvent` / `PayloadJson` ベースの Application event model を廃止する。

これらは Domain Event ではない。

Application が cross-session observable event として公開し、
Infrastructure が persistence representation に変換して保存する event とする。

Application は JSON を扱わない。

### Target model

Application に共通 metadata のみを持つ event base type を置く。

例:

```csharp
public abstract record EventFeedItem(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId);

public abstract record RfqEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId)
    : EventFeedItem(EventId, OccurredAt, ActorUserId);

public abstract record QuoteEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    QuoteId QuoteId)
    : EventFeedItem(EventId, OccurredAt, ActorUserId);
```

各 event は semantic subtype とする。

例:

```csharp
public sealed record RfqClosedHitEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    QuoteId QuoteId)
    : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);

public sealed record RfqContactOwnerChangedEvent(
    long EventId,
    DateTimeOffset OccurredAt,
    UserId? ActorUserId,
    CaseId CaseId,
    UserId From,
    UserId To)
    : RfqEvent(EventId, OccurredAt, ActorUserId, CaseId);
```

現在 persistence/feed で扱っている RFQ / Quote transition kind を確認し、
既存 semantics に対応する concrete event subtype を定義する。

新しい event semantics を追加しない。

---

## 2. Remove Type + PayloadJson from Application

Application event に以下を残さない。

```csharp
Type
PayloadJson
```

また、

```csharp
EventType + EventPayload
```

のように type と payload を独立に保持し、
不整合な組み合わせを作れる構造にも変更しない。

event kind は concrete subtype から決まる。

`IEventFeed` は JSON persistence model ではなく typed Application event を返す。

`EventsController` が API response を組み立てる際も、
Application event の concrete subtype を基に response を生成する。

Application から persistence JSON string を API へそのまま露出しない。

---

## 3. Pending / Write-side Events

現在 `PersistedEventSink` 等で保持している pending event から `PayloadJson` を除去する。

transition から typed pending event / semantic event を生成し、
Infrastructure が DB write の直前に persistence representation へ変換する。

概念的には以下とする。

```text
Domain/Application transition
    -> typed semantic event
    -> Infrastructure persistence mapper
    -> stable type code + persistence payload DTO
    -> JSON
    -> DB
```

write-side でも Application / Domain が JSON を生成しない。

---

## 4. Stable Event Persistence Type Codes

現在の DB schema は変更しない。

既存の

```text
events
rfq_events(type, payload)
quote_events(type, payload)
```

を維持する。

ただし DB の `type` value を enum `.ToString()` や CLR class name から生成しない。

Infrastructure に explicit mapping を置く。

例:

```text
rfq.closed-hit
rfq.closed-away
rfq.contact-owner-changed
quote.confirmed
```

実際の code value は、既存 persisted data との互換性を確認して決める。

既存 DB value をそのまま stable contract として固定する方が安全な場合は、
新しい文字列へ変更せず既存 value を明示 mapping として採用する。

重要なのは、

```csharp
transitionKind.ToString()
event.GetType().Name
```

に persistence contract を依存させないこと。

CLR type / enum rename で DB contract が変化しない構造にする。

---

## 5. Event Read Mapping

`EfCoreEventFeed` は以下の責務を持つ。

```text
DB event row
    -> explicit type code lookup
    -> persistence payload DTO deserialize
    -> typed Application event
```

Application に以下を返さない。

- raw `PayloadJson`
- DB entity
- persistence DTO
- raw persistence type code

unknown type / unsupported payload は fail-fast とする。

unknown event wrapper や silent fallback は作らない。

---

## 6. Event Payload Content

payload JSON には event-specific data のみ保存する。

DB column / parent event row に既に存在する値を payload に重複させない。

原則 payload に含めないもの:

- EventId
- OccurredAt
- ActorUserId
- CaseId / QuoteId
- event type code

例:

```json
{
  "from": "sales-a",
  "to": "sales-b"
}
```

ただし現在の persistence semantics 上、event-specific relation として別 identity が必要な場合
（例: hit event における closed QuoteId 等）は payload DTO に含めてよい。

---

## 7. EOD Query and Event Type Contract

現在 `EfCoreEodQueries` が persisted RFQ event type を参照している。

現在のような

```csharp
RfqTransitionKind.ClosedHit.ToString()
RfqTransitionKind.ClosedAway.ToString()
```

を persistence type code として使用しない。

04で定義する stable event persistence type code と同じ explicit mapping / constants を使用する。

EOD query と event serializer がそれぞれ独立に type string を組み立てない。

同じ persistence contract definition を参照する。

EOD の business semantics、desk filtering、business-date boundary は変更しない。

---

## 8. Invalid Persisted Events

以下は fail-fast とする。

- unknown event type code
- unsupported event payload version
- malformed JSON
- required payload field missing
- invalid typed identity
- payload shape mismatch

silent defaulting / unknown event object / raw JSON fallback は導入しない。

---

## 9. GridConfig — Keep Current Boundary

03追加対応で以下は既に実装済み。

```text
IGridConfigStore
EfCoreGridConfigStore
```

GridConfig payload は Application / Infrastructure boundary で opaque string として扱う。

04ではこの設計を維持する。

以下を追加しない。

- AG Grid-specific DTO
- backend-side column/filter/pin/order interpretation
- backend-side JSON schema migration
- polymorphic GridConfig model

`ScreenId` / `ConfigKey` は string のままとする。

現在の `Version` と JSON schema version を混同しない。

GridConfig persistence は今回の versioned business DTO 対象には含めない。

---

## 10. WorkingQuote / ConfirmedQuote Business JSON

### Intent

WorkingQuote / ConfirmedQuote の payload は business data なので、
GridConfig のような opaque JSON として Domain/Application に渡さない。

Domain/Application は current typed model のみを持つ。

JSON serialization contract は Infrastructure が所有する。

### Current issue

現在 repository / mapper 内に以下が残っている場合は除去する。

```csharp
JsonSerializer.Serialize(domainPayload)
JsonSerializer.Deserialize<DomainPayload>(json)
```

Domain type を persistence JSON contract として直接 serialize しない。

---

## 11. Versioned Persistence DTO

Infrastructure に persistence-only DTO を作る。

例:

```csharp
internal abstract record QuotePayloadDto;

internal sealed record ManualQuotePayloadDtoV1(...)
    : QuotePayloadDto;

internal sealed record CalculatedQuotePayloadDtoV1(...)
    : QuotePayloadDto;

internal sealed record CalculatedQuotePayloadDtoV2(...)
    : QuotePayloadDto;
```

DTO hierarchy は root schema version ごとの tree にしない。

以下のような構造は作らない。

```text
QuotePayloadDtoV1
  ManualV1
  CalculatedV1

QuotePayloadDtoV2
  ManualV2
  CalculatedV2
```

variant shape が変わった場合のみ、その variant の新 DTO version を追加する。

例:

```text
QuotePayloadDto
  ManualQuotePayloadDtoV1
  CalculatedQuotePayloadDtoV1
  CalculatedQuotePayloadDtoV2
```

Manual payload が変わっていなければ、
Calculated が V2 になっても `ManualQuotePayloadDtoV1` をそのまま使用する。

---

## 12. Preserve Existing DB Layout

現在 WorkingQuote / ConfirmedQuote が

```text
calculated_payload
manual_payload
```

のように separate jsonb columns を持っている場合、
04のためだけに unified payload column へ変更しない。

DB schema / migration は変更しない。

versioned DTO は現在の column layout に合わせて実装する。

必要なら各 column の JSON 内に explicit discriminator/version contract を持たせる。

DB layout の変更ではなく、
JSON contract の ownership / compatibility を整理することが目的。

---

## 13. Domain Remains Single Current Version

Domain/Application に persistence version type を追加しない。

```text
ManualQuotePayloadDtoV1 --------\
CalculatedQuotePayloadDtoV1 -----> current Domain model
CalculatedQuotePayloadDtoV2 ----/
```

read:

```text
persisted DTO version
    -> Infrastructure mapper
    -> current Domain payload
```

write:

```text
current Domain payload
    -> latest persistence DTO
    -> JSON
```

過去 DTO version を write しない。

---

## 14. Stable Quote Payload Discriminator

polymorphic persistence contract を使用する場合、
discriminator は explicit stable value とする。

例:

```text
manual-v1
calculated-v1
calculated-v2
```

CLR class name から自動生成しない。

`.ToString()` に依存しない。

`System.Text.Json` の polymorphic serialization を利用してよい。

例:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ManualQuotePayloadDtoV1), "manual-v1")]
[JsonDerivedType(typeof(CalculatedQuotePayloadDtoV1), "calculated-v1")]
[JsonDerivedType(typeof(CalculatedQuotePayloadDtoV2), "calculated-v2")]
internal abstract record QuotePayloadDto;
```

ただし現在の separate-column structure に polymorphic root が不要なら、
無理に base class / polymorphic envelope を入れない。

その場合も各 payload format の version は explicit persistence contract として管理する。

source generator / custom converter は必要でなければ導入しない。

---

## 15. Root Schema Version

variant 自身の version で compatibility を管理できるため、
今回 root `schemaVersion` envelope を必須にしない。

root/envelope 全体の shape に breaking change が必要になった場合のみ、
将来 root version を追加する。

04では unnecessary version hierarchy を作らない。

---

## 16. Shared Quote Persistence Mapping

WorkingQuote / ConfirmedQuote で同じ payload contract を共有できる場合は、
同じ persistence DTO / mapper / serializer implementation を共有する。

repository ごとに同じ serialization logic を複製しない。

概念的に以下とする。

```text
WorkingQuoteRepository ----\
                            -> Quote persistence mapper / serializer
ConfirmedQuoteRepository --/
```

repository は aggregate persistence orchestration を担当し、
JSON contract knowledge を重複して持たない。

---

## 17. Persistence Serializer Contract

Infrastructure に persistence JSON serializer configuration を一元化する。

repository / event feed ごとに default `JsonSerializer` behavior に依存しない。

以下を明示的に固定する。

- property naming policy
- discriminator property name
- enum representation
- null handling
- number handling
- property-name case sensitivity

既存 persisted data との compatibility を優先する。

HTTP API の `JsonSerializerOptions` と persistence serializer options を共有しない。

`Program.cs` の API JSON settings を変更しても DB persistence contract が変化しない構造にする。

---

## 18. Compatibility Rules

read:

```text
stored type/discriminator/version
    -> exact persistence DTO
    -> mapper
    -> current Application/Domain type
```

write:

```text
current Application/Domain type
    -> latest persistence DTO
    -> stable persistence code/discriminator
    -> JSON
```

過去 persisted format を読む必要がある場合、
compatibility mapping は Infrastructure に置く。

Application / Domain に old persistence version を持ち込まない。

既存 development DB data について compatibility reader を維持するか、
dev DB reset を前提にするかは現在の committed data/testsを確認して判断する。

ただし既存 production-like contract を理由なく壊さない。

---

## 19. Tests for 04

04では persistence contract に直接関係する専用テストだけ追加する。

### Event contract tests

最低限:

- persisted event type + payload -> expected typed Application event
- typed Application event -> expected stable persistence type code
- unknown event type -> fail
- malformed event payload -> fail
- required event payload field missing -> fail
- common metadata が payload に不必要に重複しない
- EOD query が stable persisted event type contract を使用する

### Quote persistence tests

最低限:

- old/current V1 JSON -> current Domain payload
- current Domain payload -> latest persistence DTO/version
- unchanged variant は既存 DTO version をそのまま利用する
- unknown discriminator/version -> fail
- malformed JSON -> fail
- WorkingQuote payload round-trip
- ConfirmedQuote payload round-trip

### GridConfig

新しい専用テストは不要。

既存 opaque string behavior を壊さないことだけ維持する。

### Full regression

04で API 全体・構造全体の追加回帰テストは作らない。

05まで構造整理が続くため、
全体的な回帰確認・必要な API integration test の追加は05終了時にまとめて行う。

既存 tests は引き続き通る状態を維持する。

---

## 20. Do Not Change

今回以下は変更しない。

- Domain lifecycle model
- RFQ / Quote transition semantics
- DB table / column structure
- migrations
- API routes
- GridConfig key / storage model
- GridConfig payload format
- event cursor ordering / locking semantics
- PostgreSQL transaction/concurrency behavior
- business-date / EOD semantics
- 03 / 03b / 03追加で整理した folder structure

04は event/persistence JSON contract の整理に限定する。

---

## Done When

- Application event に `PayloadJson` が存在しない
- event feed が typed semantic event を返す
- event persistence type code が explicit stable mapping になっている
- `.ToString()` / CLR type name が event persistence contract になっていない
- `EfCoreEodQueries` が同じ stable persisted event type contract を使用している
- event payload が event-specific data のみを保持している
- GridConfig は `IGridConfigStore` 経由の opaque string のまま
- WorkingQuote / ConfirmedQuote が Domain payload を直接 JSON serialize していない
- Infrastructure に versioned persistence DTO / mapper がある
- variant shape が変わった場合のみ新 DTO version を追加する構造になっている
- existing separate jsonb column layout が維持されている
- persistence serializer options が Infrastructure に一元化されている
- API JSON settings と persistence JSON settings が分離されている
- old/current persisted version -> current Domain conversion がテストされている
- unknown discriminator/type / malformed persisted data が fail-fast する
- existing tests が通る
