# Splice — Server Contract: Wallet, Escrow, Snapshot & Database

สถานะ: **Architecture Proposal v1**  
วันที่: 2026-07-22  
ขอบเขต: Prototype C → Production Vertical Slice

## 1. เป้าหมาย

เอกสารนี้กำหนด contract กลางสำหรับ:

- Wallet: Premium Diamond, War Gem และ Meta Gold
- Raid/Town Escrow
- Immutable Town Snapshot
- Raid Session และ authoritative settlement
- PostgreSQL schema, idempotency, concurrency และ recovery
- API ที่ Unity client, Meta Server และ Raid Server ใช้ร่วมกัน

หลักสำคัญ: **client ส่งได้เฉพาะ intent และไม่ส่งยอดเงิน, ราคา, reward หรือผลลัพธ์ที่ server เชื่อได้โดยตรง**

## 2. Decisions ที่ควรล็อก

1. **Premium Diamond แยกจาก War Gem** และไม่ถูกขโมยจากผู้เล่น offline
2. **War Gem** เป็น stake currency ที่หาได้จาก gameplay และอยู่ใน escrow ได้
3. **Meta Gold** ใช้ checkout/upgrade เมือง; ไม่ใช่ Gold ในแมตช์
4. **Match Gold** รีเซ็ตทุก Raid และไม่บันทึกใน Wallet DB
5. จำนวนเงินทุกชนิดใช้ integer (`BIGINT`) ห้ามใช้ float
6. Snapshot ที่ Raid อ้างอยู่ต้อง immutable; deploy revision ใหม่ไม่แก้ revision เก่า
7. การ fund/refund/settle ต้องทำใน database transaction เดียวและ idempotent
8. Raid ที่มีผลต่อ shared economy ต้องใช้ผลจาก **trusted Raid Server** หรือระบบ verify/re-simulate ฝั่ง server เท่านั้น

## 3. Production Authority Model

```text
Unity Client
    │ intent + auth token + idempotency key
    ▼
Meta API
    ├─ Wallet/Ledger
    ├─ Snapshot/Target Pool
    ├─ Matchmaking/Protection
    └─ Raid Orchestration
            │ signed raid ticket
            ▼
      Authoritative Raid Server
            │ signed result / server credential
            ▼
        Settlement Service
            │ one DB transaction
            ▼
          PostgreSQL
```

### Local-host boundary

- Raid ฟรี/PvE ที่ไม่แตะ shared economy ยังเล่นแบบ local host ได้
- Raid ที่มี stake, defender loss, leaderboard หรือ reward ซื้อขายได้ **ห้ามเชื่อผล local host**
- ทาง production ที่ปลอดภัยที่สุดคือ dedicated/headless authoritative Raid Server
- deterministic replay verification เป็นทางเลือกภายหลัง แต่ Unity physics/gameplay ปัจจุบันยังไม่ควรถือว่า deterministic พอ

## 4. Currency Contract

| Currency | ใช้ทำอะไร | ขโมย/Stake ได้ | Authority |
|---|---|---:|---|
| `PREMIUM_DIAMOND` | IAP, cosmetic, convenience ที่มีเพดาน | ไม่ได้ | Meta Server + receipt validation |
| `WAR_GEM` | Raid stake, defender vault escrow | ได้ตามกติกา/เพดาน | Meta Server ledger |
| `META_GOLD` | Checkout เมือง, upgrade, expansion | ไม่โอนตรงระหว่างผู้เล่น | Meta Server ledger |
| `MATCH_GOLD` | Deploy unit/miner ภายใน Raid | ไม่บันทึกข้าม Raid | Raid Server memory |

กติกา Wallet:

- Server เป็นผู้คำนวณ delta ทุกครั้ง
- Client อ่าน balance และส่ง action เช่น `confirm raid` หรือ `checkout draft`
- ห้ามมี API รูปแบบ `addCurrency(amount)` จาก client
- Ledger เป็น append-only; การแก้ยอดใช้ reversal transaction ไม่แก้ประวัติเดิม
- Admin grant ทุกครั้งต้องมี actor, reason, ticket และ audit log

## 5. Ledger Model

แนะนำ **double-entry ledger** เพื่อให้ทุก transaction ตรวจได้ว่าผลรวมต่อ currency เท่ากับศูนย์

ตัวอย่าง Confirm Raid stake 100:

```text
Player WAR_GEM account       -100
Raid Escrow WAR_GEM account  +100
                               ---
                                 0
```

ตัวอย่าง Full Victory gross payout 180:

```text
Raid Escrow                  -100
System Reward Pool            -80
Player Wallet                +180
                              ---
                                0
```

Reward ที่มากกว่า stake จึงต้องมาจาก System Reward Pool อย่างชัดเจน ไม่สร้างเงินแบบไม่มีที่มา

### Ledger invariants

- `SUM(posting.amount)` ต่อ transaction/currency ต้องเป็น 0
- Ledger account balance ห้ามติดลบ ยกเว้น system account ที่กำหนดไว้
- หนึ่ง business operation มี `idempotency_key` เดียว
- transaction ที่ `posted` แล้วห้ามแก้ postings
- refund ใช้ transaction ใหม่อ้าง `reversal_of_transaction_id`

## 6. Escrow Contract

### Raid Escrow state machine

```text
QUOTED
  └─ FUNDING
       ├─ FUNDED ──► ACTIVE ──► SETTLING ──► SETTLED
       └─ FAILED

FUNDED ── startup failure/expiry ──► REFUNDED
ACTIVE ── authoritative abort policy ──► SETTLING
```

สถานะที่อนุญาต:

- `QUOTED`: ราคาและ payout ถูก server คำนวณ มีเวลาหมดอายุ
- `FUNDED`: stake ย้ายจาก wallet เข้า escrow แล้ว
- `ACTIVE`: Raid Server รับ ticket และเริ่ม gameplay แล้ว
- `SETTLING`: lock เพื่อคำนวณผลครั้งเดียว
- `SETTLED`: ledger postings ครบและจบถาวร
- `REFUNDED`: gameplay ไม่เริ่มและคืน stake แล้ว
- `FAILED`: fund ไม่สำเร็จ ไม่มี gameplay

### Defender Town Escrow

เมื่อกด Ready to Defend:

1. Server validate committed layout และ base rating
2. Server กำหนดช่วง defender stake ที่เลือกได้
3. ผู้เล่นเลือก stake ภายในช่วง
4. เงินย้ายจาก Player Wallet → Town Escrow
5. สร้าง immutable snapshot และเปิด deployment ใน target pool

เมื่อ undeploy:

- ห้ามคืน escrow หากยังมี Raid ที่ `FUNDED/ACTIVE/SETTLING` อ้าง deployment นี้
- เมื่อไม่มี Raid ค้าง เงินที่เหลือใน Town Escrow จึงคืน Wallet
- Core แตกให้หักเฉพาะ loss cap; เมือง/collection ไม่ถูกลบ

### Settlement rules

- Amount ทุกตัวมาจาก `raid_quote` ที่ server ล็อกไว้ ไม่อ่านค่าจาก client
- Result มาจาก trusted Raid Server credential และต้องตรง `raid_id`
- Settlement มี unique constraint ต่อ `raid_id`
- duplicate result คืน response เดิมและไม่สร้าง postings เพิ่ม
- result หลัง `SETTLED/REFUNDED` ถูก reject และบันทึก security event

## 7. Snapshot Contract

แยกข้อมูลเป็น 3 ชั้น:

1. **Draft** — แก้ได้, ยังไม่เสียเงิน, ใช้ optimistic version
2. **Committed Layout** — checkout แล้ว, server คำนวณราคา/ownership/content
3. **Deployed Snapshot** — immutable revision ที่ target pool และ Raid อ้าง

### Snapshot payload ที่แนะนำ

```json
{
  "schemaVersion": 2,
  "contentVersion": "2026.07.1",
  "sceneContractVersion": "raid-map-01.v3",
  "factionId": "natural",
  "baseLevel": 7,
  "gridVersion": 1,
  "towers": [
    {
      "instanceId": "uuid",
      "contentId": "natural/thorn_snare_t1",
      "cellX": 4,
      "cellZ": 8,
      "rotationStep": 1,
      "upgradeLevels": { "attack": 2, "health": 1, "armor": 0, "range": 0, "targets": 0 }
    }
  ],
  "garrison": [
    { "instanceId": "uuid", "contentId": "natural/raptor_t1", "cellX": 6, "cellZ": 5 }
  ],
  "minerContentIds": ["natural/miner_t1"],
  "armyShowcasePresetId": "uuid",
  "heroAppearanceId": "hero_skin_id"
}
```

### สิ่งที่เปลี่ยนจาก local prototype

- Production ควรเก็บตำแหน่งเป็น integer grid cell ไม่ใช้ world-space float
- `storedGold`/War Gem balance ไม่ฝังเป็น authority ใน snapshot
- Economy ปัจจุบันของเมืองอยู่ใน `town_vaults`; ตอนสร้าง quote server ล็อก loot preview/maximum ไว้ใน Raid Quote
- `basePowerRating`, cost และ capacity คำนวณใหม่จาก server content catalog
- Client-sent rating, cost, owner และ validation result เป็นข้อมูลที่เชื่อไม่ได้

### Immutability rules

- ห้าม `UPDATE payload` ของ `town_snapshots`
- revision ใหม่ = `INSERT` row ใหม่
- `town_deployments.active_snapshot_id` เปลี่ยน pointer ไป revision ใหม่ได้
- Raid ที่สร้างแล้วเก็บ `target_snapshot_id` เดิมตลอดจน settlement
- snapshot เก่าลบไม่ได้ขณะมี Raid/Report อ้าง; หลัง retention period ใช้ archive แทน hard delete
- เก็บ `payload_sha256`, `validator_version`, `content_version` เพื่อ audit/replay

## 8. PostgreSQL Schema

PostgreSQL เป็น canonical source of truth เพราะ Wallet/Escrow/Snapshot ต้อง commit หลายตารางแบบ atomic โดย transaction block; financial path ใช้ row locks หรือ `SERIALIZABLE` และต้อง retry เมื่อเกิด serialization failure ตามเอกสาร PostgreSQL

### 8.1 Core identity

```sql
players (
  player_id uuid primary key,
  auth_subject text unique not null,
  status varchar(24) not null,
  created_at timestamptz not null
)

towns (
  town_id uuid primary key,
  owner_player_id uuid not null references players,
  faction_id varchar(80) not null,
  base_level int not null,
  draft_version bigint not null,
  created_at timestamptz not null,
  unique (owner_player_id, faction_id)
)
```

### 8.2 Wallet and ledger

```sql
ledger_accounts (
  ledger_account_id uuid primary key,
  owner_type varchar(24) not null,  -- PLAYER, RAID_ESCROW, TOWN_ESCROW, TOWN_VAULT, SYSTEM
  owner_id uuid not null,
  currency varchar(32) not null,
  balance bigint not null,
  version bigint not null,
  created_at timestamptz not null,
  unique (owner_type, owner_id, currency)
)

ledger_transactions (
  ledger_transaction_id uuid primary key,
  idempotency_key varchar(160) not null,
  transaction_type varchar(40) not null,
  reference_type varchar(40) not null,
  reference_id uuid not null,
  status varchar(16) not null,       -- PENDING, POSTED, REVERSED
  reversal_of_transaction_id uuid null references ledger_transactions,
  metadata jsonb not null,
  created_at timestamptz not null,
  posted_at timestamptz null,
  unique (idempotency_key)
)

ledger_postings (
  posting_id uuid primary key,
  ledger_transaction_id uuid not null references ledger_transactions,
  ledger_account_id uuid not null references ledger_accounts,
  currency varchar(32) not null,
  amount bigint not null check (amount <> 0),
  balance_after bigint not null,
  created_at timestamptz not null
)
```

`SUM(amount)=0` เป็น cross-row invariant จึงต้อง enforce ใน posting stored procedure/service transaction ก่อนเปลี่ยนสถานะเป็น `POSTED`

### 8.3 Draft, snapshot and deployment

```sql
town_drafts (
  town_id uuid primary key references towns,
  version bigint not null,
  payload jsonb not null,
  payload_hash varchar(64) not null,
  updated_at timestamptz not null
)

town_layout_commits (
  layout_commit_id uuid primary key,
  town_id uuid not null references towns,
  revision int not null,
  payload jsonb not null,
  payload_hash varchar(64) not null,
  checkout_cost bigint not null,
  content_version varchar(40) not null,
  validator_version varchar(40) not null,
  committed_at timestamptz not null,
  unique (town_id, revision)
)

town_snapshots (
  snapshot_id uuid primary key,
  town_id uuid not null references towns,
  layout_commit_id uuid not null references town_layout_commits,
  revision int not null,
  payload jsonb not null,
  payload_sha256 varchar(64) not null,
  faction_id varchar(80) not null,
  base_level int not null,
  base_power bigint not null,
  used_capacity int not null,
  max_capacity int not null,
  tower_count int not null,
  garrison_count int not null,
  content_version varchar(40) not null,
  validator_version varchar(40) not null,
  committed_at timestamptz not null,
  unique (town_id, revision)
)

town_vaults (
  town_id uuid not null references towns,
  currency varchar(32) not null,
  ledger_account_id uuid unique not null references ledger_accounts,
  lootable_cap bigint not null,
  version bigint not null,
  updated_at timestamptz not null,
  primary key (town_id, currency)
)

town_escrows (
  town_escrow_id uuid primary key,
  town_id uuid not null references towns,
  ledger_account_id uuid unique not null references ledger_accounts,
  currency varchar(32) not null,
  funded_amount bigint not null,
  state varchar(24) not null,       -- FUNDED, ACTIVE, RETIRING, REFUNDED
  funded_transaction_id uuid not null references ledger_transactions,
  refunded_transaction_id uuid null references ledger_transactions,
  created_at timestamptz not null,
  refunded_at timestamptz null
)

town_deployments (
  deployment_id uuid primary key,
  town_id uuid not null references towns,
  active_snapshot_id uuid not null references town_snapshots,
  town_escrow_id uuid not null references town_escrows,
  status varchar(24) not null,       -- READY, ACTIVE, PAUSED, SHIELDED, RETIRED
  stake_band varchar(24) not null,
  shield_until timestamptz null,
  activated_at timestamptz not null,
  retired_at timestamptz null
)
```

ใช้ unique partial index ให้หนึ่งเมืองมี deployment ที่ active ได้เพียงรายการเดียว

```sql
create unique index uq_one_active_deployment_per_town
on town_deployments(town_id)
where status in ('READY', 'ACTIVE', 'PAUSED', 'SHIELDED');
```

### 8.4 Quote, raid and escrow

```sql
raid_quotes (
  quote_id uuid primary key,
  attacker_player_id uuid not null references players,
  target_deployment_id uuid not null references town_deployments,
  target_snapshot_id uuid not null references town_snapshots,
  attacker_loadout_id uuid not null,
  difficulty_band varchar(16) not null,
  attacker_stake bigint not null,
  defender_max_loss bigint not null,
  full_victory_payout bigint not null,
  outer_payout bigint not null,
  inner_payout bigint not null,
  core_payout bigint not null,
  rules_version varchar(40) not null,
  expires_at timestamptz not null,
  created_at timestamptz not null
)

raid_sessions (
  raid_id uuid primary key,
  quote_id uuid unique not null references raid_quotes,
  attacker_player_id uuid not null references players,
  defender_player_id uuid not null references players,
  target_snapshot_id uuid not null references town_snapshots,
  state varchar(24) not null,        -- PREPARED, FUNDED, ACTIVE, SETTLING, SETTLED, REFUNDED
  scene_contract_version varchar(40) not null,
  raid_server_id varchar(120) null,
  started_at timestamptz null,
  completed_at timestamptz null,
  created_at timestamptz not null,
  check (attacker_player_id <> defender_player_id)
)

raid_escrows (
  escrow_id uuid primary key,
  raid_id uuid unique not null references raid_sessions,
  ledger_account_id uuid unique not null references ledger_accounts,
  currency varchar(32) not null,
  funded_amount bigint not null,
  state varchar(24) not null,
  funded_transaction_id uuid not null references ledger_transactions,
  settlement_transaction_id uuid null references ledger_transactions,
  refunded_transaction_id uuid null references ledger_transactions,
  created_at timestamptz not null,
  settled_at timestamptz null
)

raid_results (
  raid_id uuid primary key references raid_sessions,
  outcome varchar(24) not null,
  end_reason varchar(40) not null,
  breached_rings int not null,
  secured_loot bigint not null,
  payout bigint not null,
  result_sequence bigint not null,
  result_hash varchar(64) not null,
  raid_server_id varchar(120) not null,
  received_at timestamptz not null,
  settled_at timestamptz null
)
```

### 8.5 Protection, reports and reliability

```sql
pair_raid_limits (
  attacker_player_id uuid not null references players,
  defender_player_id uuid not null references players,
  raid_count_24h int not null,
  reward_multiplier_millis int not null,
  cooldown_until timestamptz null,
  updated_at timestamptz not null,
  primary key (attacker_player_id, defender_player_id)
)

defense_reports (
  report_id uuid primary key,
  raid_id uuid unique not null references raid_sessions,
  defender_player_id uuid not null references players,
  attacker_public_profile jsonb not null,
  summary jsonb not null,
  revenge_expires_at timestamptz null,
  created_at timestamptz not null
)

idempotency_requests (
  scope_key varchar(200) not null,
  idempotency_key varchar(160) not null,
  request_hash varchar(64) not null,
  response_status int null,
  response_body jsonb null,
  expires_at timestamptz not null,
  created_at timestamptz not null,
  primary key (scope_key, idempotency_key)
)

outbox_events (
  event_id uuid primary key,
  aggregate_type varchar(40) not null,
  aggregate_id uuid not null,
  event_type varchar(80) not null,
  payload jsonb not null,
  created_at timestamptz not null,
  published_at timestamptz null,
  attempt_count int not null default 0
)
```

Outbox event ต้อง insert ใน transaction เดียวกับ ledger/snapshot/settlement แล้ว worker ค่อย publish เพื่อไม่ให้เกิดกรณี DB commit แต่ event หาย

## 9. Required Indexes

- `ledger_postings(ledger_account_id, created_at desc)`
- `ledger_transactions(reference_type, reference_id)`
- `town_snapshots(town_id, revision desc)`
- `town_deployments(status, shield_until, activated_at)`
- `town_snapshots(faction_id, base_power)`
- `raid_quotes(attacker_player_id, expires_at)`
- `raid_sessions(attacker_player_id, created_at desc)`
- `raid_sessions(defender_player_id, created_at desc)`
- `raid_sessions(target_snapshot_id)`
- `pair_raid_limits(defender_player_id, cooldown_until)`
- partial index `outbox_events(created_at) where published_at is null`

ใช้ GIN index บน JSONB เฉพาะ field ที่ต้อง query จริง; metadata หลักที่ใช้ matchmaking ควรแยกเป็น typed columns

## 10. API Contract

Mutating request ทุกตัวต้องมี:

```http
Authorization: Bearer <access-token>
Idempotency-Key: <client-generated-uuid>
X-Request-Id: <trace-uuid>
Content-Type: application/json
```

### Wallet

```http
GET /v1/wallet
GET /v1/wallet/ledger?currency=WAR_GEM&cursor=...
```

Client เห็น transaction history แต่ไม่สามารถส่ง delta

### Draft checkout

```http
PUT  /v1/towns/{townId}/draft
POST /v1/towns/{townId}/checkout
```

Checkout request:

```json
{
  "expectedDraftVersion": 14,
  "expectedTownRevision": 8
}
```

Server โหลด draft, resolve content IDs, คำนวณ net cost, lock Meta Gold account, post ledger และสร้าง layout commit ใน transaction เดียว

### Deploy snapshot

```http
POST /v1/towns/{townId}/deployments
```

```json
{
  "layoutCommitId": "uuid",
  "requestedWarGemStake": 300
}
```

Server คำนวณ allowed stake band, validate scene/content/capacity, fund Town Escrow และ insert immutable snapshot/deployment แบบ atomic

### Target and quote

```http
GET  /v1/raid-targets?band=FAIR&cursor=...
POST /v1/raid-quotes
```

```json
{
  "targetDeploymentId": "uuid",
  "attackerLoadoutId": "uuid"
}
```

Quote response ต้องมี `quoteId`, locked `snapshotId`, stake/payout ทุกระดับ, difficulty/rules version และ `expiresAt`

### Confirm Raid

```http
POST /v1/raids
```

```json
{
  "quoteId": "uuid"
}
```

ภายใน transaction เดียว:

1. lock quote/deployment/player wallet rows
2. ตรวจ quote ยังไม่หมดอายุ, snapshot ยัง valid, attacker ≠ defender, shield/cooldown
3. สร้าง raid session
4. ย้าย attacker stake เข้า Raid Escrow
5. บันทึก idempotent response/outbox event

### Technical startup refund

```http
POST /v1/raids/{raidId}/startup-refund
```

```json
{
  "raidId": "uuid",
  "reasonCode": "CLIENT_START_FAILED"
}
```

ใช้ได้เฉพาะ raid ของ caller ที่ fund แล้วแต่ trusted runtime ยังไม่ mark started; ต้องมี
`Idempotency-Key` และ server เป็นผู้ตรวจ state/คืน stake เต็ม ห้ามรับ amount หรือ payout จาก client

### Raid start/result — internal only

```http
POST /internal/v1/raids/{raidId}/started
POST /internal/v1/raids/{raidId}/results
```

เปิดเฉพาะ Raid Server ผ่าน service identity/mTLS ไม่ให้ Unity client เรียก

Result payload:

```json
{
  "resultSequence": 1,
  "outcome": "FULL_VICTORY",
  "endReason": "CORE_DESTROYED",
  "breachedRings": 3,
  "securedLoot": 120,
  "combatDigest": "sha256",
  "serverBuildVersion": "raid-2026.07.22.1"
}
```

Settlement service คำนวณ payout ใหม่จาก quote/rules; ไม่เชื่อ `payout` จาก Raid Server หรือ client

### Standard error response

```json
{
  "error": {
    "code": "QUOTE_EXPIRED",
    "message": "Raid quote has expired.",
    "requestId": "uuid",
    "retryable": false
  }
}
```

Error codes ขั้นต่ำ:

- `INSUFFICIENT_FUNDS`
- `DRAFT_VERSION_CONFLICT`
- `CONTENT_VALIDATION_FAILED`
- `STAKE_OUT_OF_BAND`
- `TARGET_INELIGIBLE`
- `SELF_TARGET_FORBIDDEN`
- `PAIR_COOLDOWN_ACTIVE`
- `QUOTE_EXPIRED`
- `IDEMPOTENCY_KEY_REUSED`
- `RAID_ALREADY_SETTLED`
- `SERIALIZATION_RETRY_REQUIRED`

## 11. Idempotency Contract

- Key scope = authenticated actor + HTTP method + route group
- เก็บ `request_hash`; key เดิมแต่ body ต่างให้ `409 IDEMPOTENCY_KEY_REUSED`
- request เดิมที่เสร็จแล้วคืน status/body เดิม
- request ที่กำลังทำให้ caller retry ด้วย backoff
- DB unique constraints เป็น safety layer สุดท้าย:
  - one quote → one raid
  - one raid → one result
  - one raid → one settlement/refund
  - one idempotency key → one ledger transaction

ตัวอย่าง deterministic keys ภายในระบบ:

```text
raid:{raidId}:fund
raid:{raidId}:startup_refund
raid:{raidId}:settlement
town:{deploymentId}:fund
town:{deploymentId}:retire_refund
checkout:{layoutCommitId}:meta_gold
```

## 12. Concurrency and Transaction Rules

- Lock ledger account rows ตาม `ledger_account_id` เรียงจากน้อยไปมากเพื่อลด deadlock
- Wallet/Escrow settlement ใช้ `SERIALIZABLE` หรือ `SELECT ... FOR UPDATE` พร้อม retry policy
- retry ทั้ง transaction เมื่อพบ serialization failure; ห้าม retry เฉพาะ statement กลางทาง
- transaction ต้องสั้น: ไม่เรียก HTTP/Raid allocation ระหว่างถือ DB lock
- allocate Raid Server หลัง fund commit; หาก allocation ล้มเหลวเรียก idempotent startup refund
- ใช้ optimistic `version` สำหรับ Draft และ profile ที่ conflict ได้โดยไม่เกี่ยวเงิน
- Redis/distributed lock ไม่ใช่ตัวคุมยอดเงิน; DB constraint/transaction คือ final authority

## 13. Failure Policy

| เหตุการณ์ | การตัดสิน |
|---|---|
| Quote หมดอายุก่อน Confirm | ไม่หักเงิน สร้าง quote ใหม่ |
| Fund สำเร็จ แต่ Raid Server ยังไม่ start | refund เต็มหลัง allocation timeout |
| Duplicate Confirm | คืน raid เดิม ไม่หักซ้ำ |
| Client disconnect หลัง ACTIVE | Raid Server เดินต่อหรือให้ reconnect; ไม่ refund อัตโนมัติ |
| Raid Server crash ก่อน gameplay checkpoint | void + refund เต็ม |
| Raid Server crash หลัง checkpoint | ใช้ policy เวอร์ชันนั้น; MVP แนะนำ void/refund เพื่อความเชื่อใจ |
| Duplicate result | คืน settlement เดิม |
| Result ขัดกับ snapshot/raid server | quarantine + security review ไม่จ่าย |
| Defender deploy revision ใหม่กลาง Raid | Raid เดิมใช้ snapshot ID เก่า |
| Settlement worker ล่มหลัง DB commit | outbox/retry อ่าน transaction เดิม ไม่จ่ายซ้ำ |

## 14. Security and Audit

- Auth token ระบุ player ID; server ไม่รับ player ID จาก body เป็น authority
- Internal result endpoint ใช้ service identity + mTLS + allowlist
- ตรวจ server build/rules version ที่อนุญาต
- Rate limit quote, confirm, checkout และ snapshot commit
- บันทึก IP/device/account risk signal แยกจาก gameplay payload
- IAP grant หลัง server-side receipt validation เท่านั้น
- มี daily mint/burn/escrow reconciliation ต่อ currency
- Alert เมื่อ ledger postings ไม่ balance, escrow ติดค้าง, payout spike หรือ pair farming ผิดปกติ
- PII แยก schema/สิทธิ์จาก gameplay และ ledger

## 15. Reconciliation Jobs

รันเป็น scheduled worker:

1. ตรวจทุก posted transaction ว่า postings รวมเป็น 0
2. เทียบ `ledger_accounts.balance` กับผลรวม postings
3. หา escrow ที่ `FUNDED` เกิน startup timeout → refund
4. หา `ACTIVE` เกิน max raid duration → incident/policy resolution
5. หา `SETTLING` ค้าง → retry ด้วย key เดิม
6. หา deployment ที่ retire แล้วแต่ Town Escrow ยังเหลือและไม่มี active raid → refund
7. สรุป currency source/sink รายวันเพื่อ balance economy

## 16. Cache, Object Storage and Backups

- PostgreSQL: source of truth
- Redis: cache target cards, rate limit, short-lived quote lookup; cache miss ต้องอ่าน DB ได้
- Object storage: replay/event stream, heatmap หรือ snapshot archive ขนาดใหญ่
- ห้ามเก็บ wallet balance หรือ settlement authority ไว้ใน Redis อย่างเดียว
- เปิด automated backup + point-in-time recovery และทดสอบ restore จริงตามรอบ

## 17. Unity-facing Interfaces

แยก interface ก่อนเปลี่ยนจาก PlayerPrefs เพื่อไม่รื้อ UI/game flow:

```csharp
public interface IWalletService
{
    Task<WalletView> GetWalletAsync(CancellationToken ct);
    Task<RaidFundingResult> FundRaidAsync(RaidFundingRequest request, string idempotencyKey, CancellationToken ct);
    Task<RaidFundingResult> CancelRaidBeforeStartAsync(string raidId, string reasonCode, string idempotencyKey, CancellationToken ct);
}

public interface ITownSnapshotService
{
    Task<TownDraftView> GetCheckedOutDraftAsync(string factionId, CancellationToken ct);
    Task SaveCheckedOutDraftAsync(BaseLayout layout, string idempotencyKey, CancellationToken ct);
    Task<SnapshotCommitResult> DeployAsync(DeployTownRequest request, string idempotencyKey, CancellationToken ct);
    Task<TownSnapshotDto> GetByIdAsync(string snapshotId, CancellationToken ct);
}

public interface IRaidContractService
{
    Task<RaidQuoteDto> CreateQuoteAsync(CreateRaidQuoteRequest request, string idempotencyKey, CancellationToken ct);
    Task<RaidStartDto> ConfirmAsync(string quoteId, string idempotencyKey, CancellationToken ct);
}

public interface IBackendTransport
{
    Task<BackendTransportResponse> SendAsync(BackendTransportRequest request, CancellationToken ct);
}
```

Implementations:

- `LocalWalletService` / `LocalTownSnapshotService`: wrap prototype ปัจจุบัน
- `RemoteWalletService` / `RemoteTownSnapshotService`: เรียก Meta API
- UI และ `RaidSceneAdapter` ใช้ interface เดียว ไม่รู้ว่า backend เป็น local หรือ remote
- Unity player transport เรียกได้เฉพาะ public `/v1/*`; ห้าม expose `/internal/*` raid result routes
- report/settlement ใน remote player composition ต้องใช้ authority guard จนกว่า C4 trusted Raid Server พร้อม

## 18. Mapping จากโค้ดปัจจุบัน

| Local Prototype | Production Contract |
|---|---|
| `LocalWarGemEconomy` | Wallet/Ledger + Raid Escrow service |
| `RaidStakeTransaction.raidId` | `raid_sessions.raid_id` |
| deterministic transaction IDs | ledger/idempotency keys เดิม |
| `RaidSessionContext` | client cache ของ server-issued RaidStart DTO |
| `TownSnapshotStore` | town snapshots repository |
| `TownDefenseSnapshot.snapshotId/revision` | immutable DB snapshot identity |
| `RaidTargetProvider` | target pool query + bot fallback service |
| `RaidSnapshotLoader` | โหลด payload ที่ signed/issued สำหรับ raid ID นั้น |
| `RaidStakeSettlementController` | trusted server result → settlement worker |

ของที่ควรรักษาไว้:

- immutable snapshot ID/revision
- target identity check ก่อน start
- deterministic fund/refund/settlement keys
- technical startup refund แยกจาก played defeat
- one settlement per raid

## 19. Implementation Order

### C0 — Boundary First

- เพิ่ม interfaces/DTOs ด้านบน
- ให้ local implementation ผ่าน tests เดิมทั้งหมด
- เปลี่ยน UI/scene adapter ให้ไม่เรียก static PlayerPrefs store โดยตรง

### C1 — PostgreSQL + Migration

- สร้าง schema migration และ local dev database
- seed system accounts/currencies
- ทำ ledger posting procedure + invariant tests
- สถานะ 2026-07-22: **IMPLEMENTED / VERIFIED แบบ local-only** ที่ `Backend/database`
- regression ครอบคลุม idempotent replay, changed-payload rejection, unbalanced direct write และ concurrent double-spend (ผ่านซ้ำ 10/10)

### C2 — Wallet/Escrow API

- wallet read, quote, fund, refund, settle
- idempotency store และ concurrency tests
- reconciliation worker
- สถานะ 2026-07-22: **Wallet/Quote/Fund/Startup Refund/Reconciliation IMPLEMENTED / VERIFIED แบบ local-only** ที่ `Backend/src/Splice.Backend.Api`
- settlement จากผลการรบยังตั้งใจรอ C4 trusted Raid Server; ไม่มี public/client settlement endpoint
- HTTP/race/restart/reconciliation regression ผ่านซ้ำ 10/10 และ dependency vulnerability scan ไม่พบช่องโหว่

### C3 — Snapshot API

- draft checkout, immutable commit/deploy, target query
- content/scene validation และ hash
- old revision retention
- สถานะ 2026-07-22: **BACKEND IMPLEMENTED / VERIFIED แบบ local-only** ที่ `Backend/src/Splice.Backend.Api/TownFeature.cs`
- server-authoritative content/capacity/cost validation, Gold checkout, Town Vault/War Gem Escrow และ immutable DB triggers ครบ
- C3 regression ครอบคลุม pre-debit rejection, payload hash, concurrent deploy, old-revision retention และ rollback ผ่านซ้ำ 10/10
- production content catalog ยัง fail-closed; ต้องเติมจาก Unity Content Catalog Exporter ใน integration step ถัดไป

### C4 — Trusted Raid Result

- raid allocation/ticket
- internal start/result endpoints
- authoritative settlement + Defense Report event

### C5 — Hardening

- pair cooldown, shield, daily loss cap, fraud cases
- load test, backup/restore drill, admin audit tools

## 20. Acceptance Criteria ก่อนใช้เงินจริง/Soft Launch

- [ ] ยิง Confirm ซ้ำ 100 ครั้ง หัก stake ครั้งเดียว
- [ ] ส่ง Result ซ้ำ/สลับลำดับ จ่ายครั้งเดียว
- [ ] server crash ทุกจุดใน fund/start/settle แล้ว reconciliation กู้ได้
- [ ] concurrent spend สองคำขอไม่ทำ balance ติดลบ
- [ ] snapshot revision ใหม่ไม่เปลี่ยน Raid ที่ active
- [ ] attacker บุก account ตัวเองไม่ได้แม้แก้ request
- [ ] client ปลอม stake/payout/outcome แล้ว server reject
- [ ] ledger ทุก transaction balance = 0
- [ ] restore database แล้ว wallet/escrow ตรงกับก่อนเหตุการณ์
- [ ] Premium Diamond ไม่เข้า loot/escrow transfer path

## 21. ข้อแนะนำสุดท้าย

เริ่มจาก **C0 interfaces** ก่อนสร้าง backend เพื่อรักษาความเร็วปัจจุบัน จากนั้นทำ PostgreSQL ledger + idempotency เป็นแกนแรก อย่าเริ่มด้วย World Map หรือ UI backend เพราะหาก Wallet/Escrow contract ยังไม่นิ่ง ทุกระบบที่ต่อจากมันจะต้องรื้ออีกครั้ง

สำหรับ Splice จุดแบ่งที่ชัดเจนคือ:

> **Snapshot เป็นหลักฐานว่าเมืองหน้าตาอย่างไร, Quote เป็นหลักฐานว่าความเสี่ยง/รางวัลเท่าไร, Escrow เป็นหลักฐานว่าเงินถูกล็อกแล้ว และ authoritative Result เป็นหลักฐานเดียวที่มีสิทธิ์สั่ง Settlement**

## References

- [PostgreSQL — Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html)
- [PostgreSQL — Explicit Locking](https://www.postgresql.org/docs/current/explicit-locking.html)
- [PostgreSQL — JSON Types and JSONB Indexing](https://www.postgresql.org/docs/current/datatype-json.html)
- [PostgreSQL — Partial Indexes](https://www.postgresql.org/docs/current/indexes-partial.html)
