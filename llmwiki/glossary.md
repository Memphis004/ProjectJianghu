# Glossary — Wuxia: Jianghu Rising

> **Rule (AGENTS #11):** identifiers in code are **English**, taken from this table.
> Player-facing text comes from localization only, never from these names. Add a row
> here *before* introducing a new domain term.

Two namespaces meet in this file. The **game** terms (sections 1–9) are
Wuxia: Jianghu Rising's own vocabulary and start with the five stat categories; the
**template** terms (sections 10–13) are the shell every game inherits and are already
real type names in `lib/`. A term belongs to the first group the moment a stage names
a type after it.

Individual skill, gear and character names are **not** in this file: they are
localization rows (`skill.wave_palm.name`, `gear.saber_hunting_wind.name`, …) so a
translator can change them without a code change.

---

## 1. ค่าสถานะ — the five categories (`EAttrCategory`)

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| วรยุทธ์ | `Martial` | ความชำนาญอาวุธ; prototype ใช้ 4 จาก 7 ค่า |
| ลมปราณ | `Neili` | ทรัพยากรและอัตราไหลเวียนของปราณ |
| กายา | `Body` | ร่างกาย: HP, การแบก, กันผลัก |
| จิตใจ | `Mind` | ความแม่น คริ และการต้านสถานะ |
| เส้นปราณ | `Meridian` | ชีพจรตู/เริน เป็นตัวคูณของลมปราณ |
| ทักษะชีวิต | `LifeSkill` | แสดงผลอย่างเดียวใน prototype ยกเว้น `Intellect` |

### 1.1 วรยุทธ์ (`Martial`) — ค่าละหนึ่งประเภทอาวุธ

| ไทย | Code name | ใน Prototype |
| --- | --- | --- |
| หมัดฝ่ามือ | `Fist` | ใช้ |
| กระบี่ | `Sword` | ใช้ |
| ดาบ | `Saber` | ใช้ |
| อาวุธลับ | `HiddenWeapon` | ใช้ |
| ดัชนี | `Finger` | มีค่า แสดงผล (ยังไม่มีวิชา) |
| ทวน | `Spear` | มีค่า แสดงผล |
| กระบอง | `Staff` | มีค่า แสดงผล |

### 1.2 ลมปราณ (`Neili`)

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| ปริมาณปราณ | `NeiliPool` | ขนาดหลอดลมปราณสูงสุด |
| หมุนเวียนปราณ | `NeiliFlow` | อัตราฟื้นลมปราณต่อวินาที |
| พลังปราณ | `NeiliPower` | ฐานดาเมจภายใน |
| กายเบา | `Lightness` | ระยะเดิน/กระโดด, ATB, หลบ — **ค่าสถานะ** ไม่ใช่วิชา (ดู `MovementSkill`) |

### 1.3 กายา (`Body`)

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| กำลังเอว | `WaistPower` | ฐานดาเมจภายนอก, ดาเมจคริ |
| ช่วงล่าง | `LowerBody` | กันผลัก, ลดโอกาสถูกผลักถอย |
| กายแกร่ง | `Toughness` | ตัวหลักของ HP |
| เอ็นกระดูก | `Sinew` | HP และป้องกัน |

### 1.4 จิตใจ (`Mind`)

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| มุ่งมั่น | `Resolve` | ต้านสถานะ |
| สมาธิ | `Focus` | แม่นยำ, ต้านการหลบของเป้า |
| สุขุม | `Composure` | สวนกลับ, ป้องกันภายใน |
| เด็ดขาด | `Decisiveness` | เวลาเติม ATB, ความเร็วตัดสินใจของ AI |
| ปฏิภาณ | `Wit` | คริติคอล |

### 1.5 เส้นปราณ (`Meridian`)

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| ชีพจรตู | `DuPulse` | เพดานลมปราณ |
| ชีพจรเริน | `RenPulse` | ฟื้นลมปราณ, ต้านสถานะ |

### 1.6 ทักษะชีวิต (`LifeSkill`) — display-only ใน prototype

| ไทย | Code name | ไทย | Code name |
| --- | --- | --- | --- |
| ปัญญา | `Intellect` | การแพทย์ | `Medicine` |
| ความรู้ | `Lore` | พิษวิทยา | `Toxicology` |
| คุณธรรม | `Virtue` | ทำอาหาร | `Cooking` |
| เสน่ห์ | `Charm` | สุรา | `Wine` |
| วาสนา | `Fortune` | ดนตรี | `Music` |
| ใจสังหาร | `KillingIntent` | พู่กัน | `Brush` |

`Intellect` เป็นข้อยกเว้นเดียวที่มีผลจริง: คูณอัตรา XP ของวิชา (`MasteryXp`).

---

## 2. ค่าสถานะต่อสู้ที่คำนวณได้ (`CombatStatBlock`)

| ไทย | Code name | มาจาก |
| --- | --- | --- |
| HP สูงสุด | `MaxHp` | `Toughness`, `Sinew` |
| ลมปราณสูงสุด | `MaxNeili` | `NeiliPool`, `DuPulse` |
| ฟื้นปราณ/วินาที | `NeiliRegenPerSecX100` | `NeiliFlow`, `RenPulse` |
| พลังภายนอก | `ExternalPower` | วิชาอาวุธที่ถือ + `WaistPower` |
| พลังภายใน | `InnerPower` | `NeiliPower` |
| ป้องกัน | `Defense` | `Sinew`, `LowerBody` |
| ป้องกันภายใน | `InnerDefense` | `NeiliPool`, `Composure` |
| ระยะเดิน | `MoveRange` | `Lightness` |
| ระยะกระโดด | `JumpRange` | `Lightness` + วิชาตัวเบา |
| เวลาเติม ATB | `AtbFillTicks` | `Lightness`, `Decisiveness` (หน่วยเป็น tick) |
| ความเร็วกระทำ | `ActionSpeedX100` | คูณ wind-up และ recovery |
| แม่นยำ | `AccuracyX100` | `Focus` |
| หลบ | `EvasionX100` | `Lightness` |
| คริติคอล | `CritChanceX100` | `Wit` |
| ดาเมจคริ | `CritDamageX100` | `WaistPower` |
| ต้านสถานะ | `StatusResistX100` | `Resolve`, `RenPulse` |
| ต้านผลัก | `KnockbackResist` | `LowerBody` |
| สวนกลับ | `CounterChanceX100` | `Composure` |
| ค่าเหนื่อยของกระโดด | `JumpCooldownTicks` | วิชาตัวเบา |
| คะแนนยุทธ์ | `PowerScore` | ผลรวมถ่วงน้ำหนักของค่าสถานะ |
| ระดับฝีมือ | `PowerRank` | 9 ขั้นจาก `PowerScore` |

**Partial values are integers.** ทุกค่าที่มีจุดทศนิยมใช้ `X100` (คูณ 100) เว้นแต่ชื่อลงท้าย
`Permille` (คูณ 1000) — ห้ามใช้ `float` ใน state ที่ต้องแฮช (AGENTS #5).

แหล่งเดียวของสูตรคือ `stat_formula` ใน `data/` (AGENTS #6) — ชื่อคอลัมน์ในตารางใช้
`Code name` จากตารางนี้ตรงตัว

---

## 3. วิชา อาวุธ และ loadout

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| วิชา | `Skill` | แถวในตาราง `skill` |
| วิชาภายนอก | `ExternalSkill` / `SkillKind.External` | สกิลที่กด D ใช้ ต้องตรงประเภทอาวุธ |
| วิชาภายใน | `InnerSkill` / `SkillKind.Inner` | passive 1 ช่อง กำหนดธาตุพลังภายใน |
| วิชาตัวเบา | `MovementSkill` / `SkillKind.Movement` | passive 1 ช่อง: ระยะ, ATB, หลบ, ผลตอนกระโดด |
| ระดับวิชา | `MasteryLevel` | 1–10; ขั้น 5 และ 10 ให้ผลพิเศษ |
| ความชำนาญ | `MasteryXp` | สะสมจากการใช้วิชา; "ยิ่งใช้ยิ่งเก่ง" |
| การใช้วิชา | `SkillUseStat` | ส่งออกจาก `ArenaMatchResult` เพื่อป้อน `StatGrowth` |
| การเติบโต | `StatGrowth` | API กลาง (`ApplyMastery`) เตรียมให้ sandbox |
| ช่องวิชา | `SkillSlot` | 0–3 ภายนอก (`MaxSkillSlots = 4`) |
| ชุดสวมใส่ | `Loadout` | แบบ A: 4+1+1 วิชา, อาวุธ 1, ชุด 1, เครื่องประดับ 2 |
| ประเภทอาวุธ | `EWeaponType` | `Fist`, `Sword`, `Saber`, `HiddenWeapon` |
| ถุงมือ | `Gauntlet` | gear ที่ `WeaponType = Fist` (มือเปล่าก็ใช้วิชาหมัดได้) |
| อาวุธ | `Weapon` / `GearSlot.Weapon` | กำหนดวิชาที่ใช้ได้ + พลังพื้นฐาน |
| ชุดเกราะ | `Armor` / `GearSlot.Armor` | ป้องกันและ HP |
| เครื่องประดับ | `Accessory` / `GearSlot.Accessory` | 2 ช่อง ค่าสถานะย่อย |
| คัมภีร์วิชา | skill manual | รางวัลชนิดหนึ่ง: ปลดล็อกวิชาใหม่ |
| โบนัสจากแหล่ง | `AttrMod` | `{ SourceId, Attribute, Value }` สำหรับ tooltip "+3 จาก …" |
| กองโบนัส | `ModifierStack` | รวมฐาน/วิชา/gear/ยาเม็ด โดยเก็บแหล่งไว้ |
| พจน์สูตร | `StatTerm` | `{ Attribute, Coefficient, Min, Max }` หนึ่งพจน์ใน `stat_formula` |

**ข้อห้าม:** ชื่อวิชา ข้อความ และ UI ของเกมอ้างอิงห้ามนำมาใช้ (AGENTS #12) วิชาทุกวิชาใน
`data/` ต้องเป็นชื่อที่ทีมเขียนขึ้นเอง

---

## 4. ATB และการต่อสู้

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| หลอด ATB | `Atb` | 0..`AtbFull` เป็น fixed-point |
| ATB เต็ม | `AtbFull` | 10,000 (ค่าใน `constant`) |
| โหมดแอคทีฟ | `AtbMode.Active` | ค่าเริ่มต้น: ศัตรูไม่รอ |
| โหมดรอ | `AtbMode.Wait` | หยุดเมื่อผู้เล่นเลือกคำสั่ง (เพื่อการเข้าถึง) |
| เฟสการแข่ง | `ArenaPhase` | `Intro` → `Running` → (`AwaitingPlayer`) → `Finished` |
| สถานะนักสู้ | `FighterState` | `Charging`, `Ready`, `Moving`, `Jumping`, `WindUp`, `Recovery`, `Stunned`, `Defeated` |
| คำสั่ง | `ArenaCommand` | `Move`, `UseSkill`, `Jump`, `Meditate`, `Surrender` (+ `Dev*`) |
| เดิน | `Move` | 4 ทิศ Manhattan, ใช้ ATB 100% |
| ใช้วิชา | `UseSkill` | เลือกช่อง 0–3 แล้วเลือกเป้า, ใช้ ATB 100% |
| กระโดด | `Jump` | ข้ามคนได้ + i-frame ระหว่างลอย, ใช้ ATB ~50% |
| ปรับลมปราณ | `Meditate` | ฟื้นลมปราณทันที, ใช้ ATB 100% |
| ยอมแพ้ | `Surrender` | จบแบบ `Aborted` |
| เตรียมท่า | `WindUp` | ช่วงที่ศัตรูเห็นวงเตือนและหลบได้ |
| ผลเกิด | Resolve | จุดที่ตัดสิน hit/evade/ดาเมจ (ตอนจบ `WindUp`) |
| ช่วงพัก | `Recovery` | หลังผลเกิด ก่อนกลับไป `Charging` |
| วงเตือน | telegraph | พื้นที่ที่วิชาจะลง (`PreviewSkillArea`) |
| i-frame | i-frame | ช่วงลอยตัวที่โดนไม่ได้ |
| ผลักถอย | knockback | `KnockbackTiles`; ชนขอบหรือคนแล้วหยุด |
| สวนกลับ | counter | เมื่อตีจากด้านหน้า |
| ด้านข้าง | flank | โบนัสเล็ก (`ArenaEventFlags.Flank`) |
| ด้านหลัง | backstab | โบนัสคริ (`ArenaEventFlags.Critical`/`Backstab`) |
| ภายใน | inner | ดาเมจสายพลังภายใน (`DamageKind.Inner`) |
| รูปพื้นที่ | `AreaShape` | `Single`, `Line`, `Cross`, `Diamond`, `Square`, `Cone` |
| การเลือกเป้า | `SkillTargeting` | `Enemy`, `Ground`, `Self` |
| เหตุผลที่คำสั่งถูกปฏิเสธ | `CommandResult` | `RejectedNotReady`, `RejectedOutOfRange`, … (ข้อความมาจาก localization) |
| เหตุจบแมตช์ | `ArenaEndReason` | `AllEnemiesDefeated`, `TimeoutHpRatio`, `TimeoutDraw`, `Surrendered`, … |
| ผลแมตช์ | `ArenaOutcome` | `Victory`, `Defeat`, `Draw`, `Aborted` (มองจากฝั่งผู้เล่น) |
| หมดเวลา | timeout | 120 วินาที; HP% สูงกว่าชนะ, เท่ากัน = เสมอ |
| วิชาใหญ่ | ultimate | `SkillFlags.Ultimate` → cut-in ชื่อวิชา |

---

## 5. สถานะผิดปกติ (`EStatus`)

| ไทย | Code name | ผล |
| --- | --- | --- |
| สกัดจุด | `AcupointSeal` | ATB หยุด |
| บาดเจ็บภายใน | `InternalInjury` | ฟื้นปราณช้าลง + DOT |
| พิษ | `Poison` | DOT |
| ชะงัก | `Stagger` | ATB ช้าลง |

---

## 6. กริด และทิศ

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| กริดประลอง | `ArenaGrid` | 12×12 จาก `arena_map` (สูงสุด `MaxGridSize = 32`) |
| ช่อง | tile / `GridCoord` | `X`,`Y` เป็น `short`; `GridCoord.Invalid` = (-1,-1) |
| ทิศ | `IsoDir` | `NE`, `SE`, `SW`, `NW` (ทแยงทั้งสี่) |
| การหัน | facing | ทิศที่นักสู้หัน; ใช้ตัดสิน flank/backstab |
| นักสู้ | fighter / `FighterView` | `MaxFighters = 8` |
| ทีม | `Team` | prototype มี 2 ทีม; 0 = ผู้เล่น |

`sprite 4 ทิศ วาด 2 ทิศแล้ว mirror` — `SE`/`NE` วาดจริง, `SW`/`NW` กลับด้าน
([ADR 0102](../docs/adr/0102-four-facing-sprites-two-drawn-mirrored.md))

---

## 7. ทัวร์นาเมนต์ เรือนพัก และ run

| ไทย | Code name | หมายเหตุ |
| --- | --- | --- |
| ทัวร์นาเมนต์ | `Tournament` / `TournamentState` | 32 ที่นั่ง, ผู้เล่นคือคนที่ 32 |
| สายการแข่ง | `Bracket` | ผังที่ผู้เล่นเห็น (ฉาก `Bracket`) |
| รอบ | `TournamentRound` | 32 → 16 → 8 → 4 → 2 |
| ผู้เข้าแข่ง | entrant | 31 NPC จาก `fighter` + ผู้เล่น |
| คู่ | match | หนึ่งคู่ในรอบ |
| คู่เบื้องหลัง | `BackgroundMatch` | คู่ที่ผู้เล่นไม่ได้แข่ง; auto-resolve แบบ headless |
| ผู้ตัดสินอัตโนมัติ | `IArenaAutoResolver` | sim จริงทั้งสองฝั่ง AI, แบ่งงานข้ามเฟรม |
| การรับผล | `ApplyMatchResult` | idempotent ด้วย settlement id |
| เรือนพัก | `Rest` | ฉากพัก: ฟื้น HP/ลมปราณเต็ม, รางวัล, loadout, หน้าตัวละคร |
| รางวัล | `RewardOffer` | เลือก 1 ใน 3; สร้างครั้งเดียวต่อรอบ |
| คลังรางวัล | `reward_pool` | ตาราง; RNG สาย `Reward` |
| ภูมิหลัง | `Background` (ตาราง `background`) | 1 ใน 4 ทางเลือกตอนสร้างตัวละคร |
| แต้มแจก | `AttributePoints` | แต้มที่ผู้เล่นแจกเองตอนสร้างตัวละคร |
| ชื่อเสียง | `Renown` | สถิติสะสมท้าย run เตรียมให้ meta ในอนาคต |
| รอบที่ไปถึง | `ReachedRound` | ผลลัพธ์หลักของหน้า `Result` |
| สถิติรวม | run summary | ดาเมจ, หลบ, สวนกลับ, วิชาที่ขึ้นขั้น |

**`Background` สองความหมาย:** ภูมิหลังตัวละคร = `Background`; คู่เบื้องหลัง =
`BackgroundMatch` ห้ามใช้คำเดียวเปล่า ๆ ในโค้ด

---

## 8. ฉาก (`SceneId`)

| ไทย | Code name | สถานะ |
| --- | --- | --- |
| บูต | `Boot` | มีแล้ว (template) |
| หน้าชื่อเกม | `Title` | มีแล้ว |
| สร้างตัวละคร | `CharacterCreate` | **เพิ่ม Stage 03** (แทนที่ `Hub` เดิมบางส่วน) |
| สายการแข่ง | `Bracket` | **เพิ่ม Stage 03** |
| เรือนพัก | `Rest` | เปลี่ยนชื่อจาก `Hub` (Stage 03) |
| ลานประลอง | `Arena` | เปลี่ยนชื่อจาก `Encounter` (Stage 03) |
| สรุปผล | `Result` | มีแล้ว |
| หน้าตัวละคร | `CharacterSheet` | หน้าจอ (ไม่ใช่ scene) Stage 11 |
| หน้า loadout | `Loadout` | หน้าจอ Stage 11 |
| หน้าข้อมูลวิชา | `SkillDetail` | หน้าจอ Stage 11 |

ตาราง mapping ฉาก placeholder → ฉากจริงอยู่ที่
[`systems/loop-and-scopes.md`](systems/loop-and-scopes.md#6-the-jianghu-mapping-stage-00)

---

## 9. สายสุ่ม (`RandomStreamKind`)

| ไทย | Code name | ใช้กับ |
| --- | --- | --- |
| สายลานประลอง | `Arena` | คำสั่ง AI, ความแปรปรวนของดาเมจ, คริ |
| สายทัวร์นาเมนต์ | `Tournament` | จัดสาย, ผลคู่เบื้องหลัง |
| สายรางวัล | `Reward` | `RewardOffer` 1 ใน 3, ยาเม็ด |
| สายภาพ | `Visual` | เอฟเฟกต์ที่ไม่กระทบ state |

ห้ามใช้ `UnityEngine.Random` / `Time` / `DateTime` ใน Core (AGENTS #5)

---

## 10. Template terms — layers and boundaries

These are the shell's own type names; a game keeps them.

| Term | Code name | Notes |
| --- | --- | --- |
| เลเยอร์พื้นฐาน | `Shared` | BCL only: RNG, clock, ticker, `Result`, ring and buffer |
| กติกา | `Domain` | plain rules and data types; no Unity, no serializer |
| ชั้นแอป | `Application` | use cases and ports; the only command surface a Presenter may call |
| การจำลอง | `Simulation` | a game's simulation; may reference Shared and Domain only |
| อะแดปเตอร์ | `Infrastructure` | implements Application ports (config, clock) |
| สัญญาภายนอก | `Contracts` | DTOs; references nothing of ours |
| โค้ดที่สร้าง | `Generated` | Luban readers, protoc output; never edited |
| พอร์ต | port | an interface declared by the layer that needs it (`IGameConfig`) |
| สัญญาลานประลอง | `ProjectJianghu.Core.Domain.Arena` | Arena contract; Presentation sees only this |

## 11. Template terms — determinism and time

| Term | Code name | Notes |
| --- | --- | --- |
| เมล็ดสุ่ม | `Seed` | one per run; every stream derives from it |
| ตัวสุ่ม | `IRandomSource` | the only randomness a rule may use; `Xoshiro128` implements it |
| สตรีมสุ่ม | `RandomStreams` | owns one source per kind |
| นาฬิกา | `IClock` | injected; rules never read `DateTime.Now` |
| ติ๊กคงที่ | `FixedTicker` | converts frame deltas into 30 Hz ticks |
| ความเร็วคงที่ | `TickRate` | 30 Hz (`ArenaConstants.TickRate`) |

## 12. Template terms — state and commands

| Term | Code name | Notes |
| --- | --- | --- |
| คำสั่ง | command | validate fully, then mutate once |
| ผลลัพธ์แบบมีชนิด | `Result` / `Result<T>` | success or a typed `Error` |
| รหัสข้อผิดพลาด | `Error.Code` | stable string codes, never sentences |
| รอบ | `RunSession` | one run: seed, streams, counter, command log |
| สถานะรอบ | `RunOutcome` | `InProgress`, `Completed`, `Lost` |
| ลำดับเหตุการณ์ | `ReplayLog` | seed + accepted commands (`ArenaReplay` per match) |
| แฮชสถานะ | `ComputeStateHash` | proves two replays are the same run |

## 13. Template terms — screens, scopes and data

| Term | Code name | Notes |
| --- | --- | --- |
| ขอบเขตแอป | App scope | `RootLifetimeScope` |
| ขอบเขตการเล่น | Run scope | `RunLifetimeScope`, owned by `RunScopeOwner` |
| ขอบเขตฉาก | Scene scope | one per scene |
| ตัวโหลดฉาก | `SceneLoader` | the one way a scene changes |
| นโยบายลำดับฉาก | `SceneFlow` | which edges exist; pure and testable |
| ตารางข้อมูล | table | a CSV under `data/tables` or `data/localization` |
| สคีมา | schema | `data/Defines/schema.xml` |
| ค่าคงที่ | `ConstantConfig` | the singleton row of tuned numbers |
| ข้อความผู้เล่น | localization row | `Key` + one column per language |
| แฮชคอนฟิก | `ConfigHash` | SHA-256 of the generated bytes |
| ตัวตรวจ | `Data.Validator` | rules a schema cannot express |
