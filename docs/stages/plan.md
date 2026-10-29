# Stage Plan — Wuxia: Jianghu Rising (v0.2)

แผนงานของเกมบนฐาน **template-v1** — Stage 00–17 ข้อกำหนดของเกมอยู่ที่
[`../gdd/GDD.md`](../gdd/GDD.md), รายงานรายสเตจอยู่ที่ `docs/stages/NN-report.md`
(แบบฟอร์ม [`_TEMPLATE.md`](_TEMPLATE.md)), และ ADR ของเกมเริ่มที่ **0100**
(เหตุผล: [ADR 0100](../adr/0100-forked-template-and-upstream-sync.md))

สถานะปัจจุบันของแต่ละสเตจอยู่ในตารางที่ [`../../knowledge.md`](../../knowledge.md#jianghu-stages)

---

## Stage 00: Fork Template และเอกสาร
ทำ Stage 00 เท่านั้น (ฉบับ v0.2): เอกสารตั้งต้นของ Wuxia: Jianghu Rising บนฐาน template-v1

(repo นี้ build และเทสต์ผ่านแล้วจาก J-Boot สเตจนี้ไม่แก้โค้ด)


งาน:

1. docs/gdd/GDD.md: GDD v0.1 จากไฟล์ที่แนบ

2. docs/stages/plan.md: Stage Plan ที่แก้ตามตาราง v0.2 แล้ว

3. llmwiki/glossary.md: คู่ไทย↔อังกฤษของค่าสถานะทุกตัว, ATB, วิชาภายนอก/ภายใน/ตัวเบา, ลมปราณ, ทัวร์นาเมนต์, เรือนพัก

4. ADR 0100 ที่มาจาก template และวิธีซิงก์ผ่าน upstream/template;

   ADR 0101 ไม่ใช้ ECS (นักสู้ไม่เกิน 8 ตัว ใช้ struct array);

   ADR 0102 sprite 4 ทิศ วาด 2 ทิศแล้ว mirror

5. knowledge.md: ตารางสถานะ Stage 00–17 ของ Jianghu และอ้างถึง template-v1

6. llmwiki/systems/loop-and-scopes.md: ตาราง mapping ฉาก placeholder → ฉากจริง

   (Hub → Rest + Bracket, Encounter → Arena, Result → Result, เพิ่ม CharacterCreate)


DoD: check-links ผ่าน, test.ps1 -Suite Fast ผ่าน, commit "stage-00: jianghu docs baseline"

-----------

## Stage 01

ทำ Stage 01 เท่านั้น: ปรับ Luban/protoc ให้เป็นของเกมใหม่
งาน:
- ล้างตาราง Survive แล้วสร้างโครง: __enums__ (EAttrCategory, EAttribute, EWeaponType, ESkillKind{External,Inner,Movement},
  EAreaShape, EStatus, EGearSlot, EAiArchetype), __beans__ (AttrMod, StatTerm, SkillLevelBonus)
- ตารางเปล่าพร้อมตัวอย่าง 1–2 แถว: attribute, stat_formula, skill, skill_level, gear, status, fighter, ai_profile,
  arena_map, tournament, reward_pool, background, constant, localization th/en
- proto/app.proto: SaveData, CharacterState, TournamentState, ArenaReplay (setup + (tick, command)[])
- validate_data.ps1 ตรวจ: อ้างอิงขาด, StatTerm อ้าง attribute ไม่มีจริง, map mask ผิดขนาด
DoD: gen idempotent (รันซ้ำ diff ว่าง), validate ผ่าน, เทสต์โหลด Tables ผ่าน


## Stage 02
ทำ Stage 02 เท่านั้น: Arena Contract + Fake + contract tests (ยังไม่ทำ sim จริง)
งาน:
1. สร้างไฟล์ตาม contract ที่แนบใน lib/Runtime/Domain/Arena/ (namespace ProjectJianghu.Core.Domain.Arena)
2. EventRing<T> ตัวกลางใน Shared (ถ้า template มี BattleEventRing ให้ refactor เป็น generic + ADR)
3. ArenaFrameBuffer: concrete IArenaFrame (double-buffer Prev/Cur ของ FighterView, cooldown array) ใช้ร่วมกันทั้ง Fake และ sim จริง
4. FakeArenaSession/Factory (Application/Arena/Fake): ATB เติมคงที่, Move/Jump วาร์ปทันที, UseSkill ดาเมจคงที่,
   options WinAtTick/LoseAtTick/TimeoutAtTick; Query ใช้ grid เปล่าและ Manhattan range
5. FakeAutoResolver: ผลตาม seed
6. ArenaReplay: เล่นซ้ำผ่าน factory ใดก็ได้แล้วเทียบ ComputeStateHash
7. ArenaSessionContractTests<TFactory> (xUnit):
   - Intro ไม่เติม ATB; Submit ระหว่าง Intro → RejectedWrongPhase
   - สั่งก่อน ATB เต็ม → RejectedNotReady; สั่งตัวที่ AI คุม → RejectedNotControlled
   - Wait mode: ผู้เล่น Ready แล้ว Phase=AwaitingPlayer และ Step ไม่เพิ่ม Tick
   - Active mode: ผู้เล่น Ready แล้ว Tick ยังเดิน และฝ่าย AI ยังทำงานได้
   - Query.Validate == ผลของ Submit ทุกกรณี (สุ่ม 5,000 คำสั่ง)
   - ทุก tile ที่ GetReachableTiles คืนมา → Move ผ่าน; tile นอกชุด → Rejected
   - หมดเวลา: HP% สูงกว่าชนะ, เท่ากัน → Draw/TimeoutDraw
   - Surrender → Aborted; dev command เมื่อ DevCheatsAllowed=false → RejectedNotAllowed
   - replay round-trip ได้ hash เท่ากัน; event Seq ต่อเนื่อง; dropped ไม่ทำให้ state เพี้ยน
   - Step/Submit/Query ไม่ allocate หลัง warm-up
8. Architecture test: public type ของ Domain.Arena ไม่อ้าง UnityEngine/R3/VContainer
9. llmwiki/systems/arena-contract.md: ตารางความหมาย DefId/Value/Value2 ต่อ event, อายุ span,
   กฎ "event ใช้แค่ VFX/เสียง/UI"
DoD: contract suite ผ่านกับ Fake ทุกข้อ, compile ใน Unity ไม่มี warning ใหม่

## Stage 03
ทำ Stage 03 เท่านั้น (ฉบับ v0.2): ปรับ Unity shell ของ template ให้เป็นของ Jianghu
(shell, scope, ฟอนต์ไทย และ Web smoke มีอยู่แล้ว ห้ามสร้างซ้ำ)

งาน:
1. ฉาก (ผ่าน ProjectSetup เท่านั้น): Hub → Rest, Encounter → Arena, เพิ่ม CharacterCreate และ Bracket แบบ placeholder
   อัปเดต SceneFlow, เทสต์ loop 20 รอบ (Rest ⇄ Arena) และ Playwright loop.spec.ts
2. HD-2D isometric spike ในฉาก Arena: ฉาก 3D placeholder + sprite billboard (point filter, PPU คงที่, pivot ที่เท้า),
   กล้อง orthographic มุม iso, pixel snap ตอนกล้องขยับ, ทดสอบ sorting ระหว่าง sprite กับ prop 3D
   สรุปเป็น ADR ใหม่ (อ่าน docs/template/examples/adr/0018 เป็นตัวอย่างการตัดสินด้วยหลักฐาน)
3. URP 3 ระดับ (Low/Med/High) พร้อมตัวเลือกใน Settings และวัด FPS ของ spike ใน browser ทุกระดับ
4. WebGL template ที่ canvas ไม่ถูก pin ไว้ที่ 960×600 รองรับ 16:9 และการ resize หน้าต่าง แล้วเขียน ADR ใหม่
   (อ่าน gotcha webgl-template-canvas-960x600 และ canvas-scaler-follows-aspect)
5. ข้อความ: ทดสอบชื่อวิชาภาษาไทยยาวที่ 1280×720; อักษรจีนตกแต่ง (เช่น 武 บนธง) ให้ทำเป็น sprite ไม่ใช่ฟอนต์
   เพื่อไม่ให้ font atlas และขนาด build บวม

DoD: PlayMode + Playwright ผ่าน, ADR ใหม่ 2 ฉบับ, ตาราง FPS ทั้ง 3 ระดับในรายงาน,
ขนาด build เทียบกับ docs/template/baseline.md

## Stage 04: ข้อมูล Prototype
ทำ Stage 04 เท่านั้น: เนื้อหา data/ ของ prototype
เนื้อหา:
- attribute: 5 หมวดต่อสู้ครบ (วรยุทธ์ใช้ 4 ค่า: หมัดฝ่ามือ/กระบี่/ดาบ/อาวุธลับ) + ทักษะชีวิตแบบ display-only
- stat_formula: derived stat = base + Σ(coef × attribute) พร้อม clamp min/max (HP เป้า 80k–120k ที่ค่ากลาง)
- skill: ภายนอก 8 (2 ต่อประเภทอาวุธ), ภายใน 3, ตัวเบา 2; skill_level 1–10 พร้อมโบนัส attribute และผลพิเศษที่ขั้น 5/10
- gear: อาวุธ 5 (รวมถุงมือ), ชุด 3, เครื่องประดับ 4; status 4 (สกัดจุด, บาดเจ็บภายใน, พิษ, ชะงัก)
- background 4 (ศิษย์สำนักกระบี่, นักมวยเร่ร่อน, ทายาทสกุลดาบ, มือสังหาร): ค่าเริ่มต้น + วิชาเริ่มต้น + แต้มแจก
- fighter 31 คน (ไล่ระดับฝีมือ 1–7) + ai_profile 4 archetype + arena_map 1 (12×12 mask) + tournament 32 + reward_pool
- constant: AtbFull, เวลาแมตช์ 120 s, ATB ที่ใช้กระโดด, K ป้องกัน, โบนัสด้านหลัง/ด้านข้าง, ค่าชำนาญต่อการใช้
DoD: gen + validate ผ่าน, เทสต์อ่านทุกตาราง, llmwiki/systems/data-schema.md, fighter ทุกคน loadout ถูกกติกาอาวุธ

## Stage 05: Character, Stats และ Loadout Domain
ทำ Stage 05 เท่านั้น: กฎตัวละคร (pure C#)
งาน:
1. CharacterState: attribute ฐาน, วิชาที่เรียน (SkillId, Level, MasteryXp), inventory ของสวมใส่, loadout
2. ModifierStack: แหล่งโบนัส (ฐาน / วิชา / gear / ยาเม็ด) แต่ละแหล่งมี source id เพื่อทำ tooltip "+3 จากฝ่ามือคลื่นซัด"
3. StatResolver: attribute สุทธิ → CombatStatBlock ตาม stat_formula; คำนวณใหม่เฉพาะเมื่อ dirty
4. PowerScore + ระดับฝีมือ 9 ขั้น; radar 5 แกน (ค่าเฉลี่ยถ่วงน้ำหนักต่อหมวด)
5. LoadoutRules: 4 ช่องภายนอก/1 ภายใน/1 ตัวเบา/อาวุธ/ชุด/เครื่องประดับ 2;
   วิชาภายนอกต้องตรงประเภทอาวุธ (typed result); เปลี่ยนอาวุธแล้ววิชาที่ไม่ตรงถูก disable ไม่ใช่ลบ
6. ArenaFighterBuilder: CharacterState → ArenaFighterSetup (contract Stage 02)
7. StatGrowth (API กลางเผื่อ sandbox): ApplyMastery(SkillUseStat[]) → XP → level up → โบนัสใหม่;
   ปัญญาเพิ่มอัตรา XP
8. CharacterCreation: background + แจกแต้มตามขอบเขต + validate
Tests: สูตรตรง data; ลบ/ใส่ gear แล้วค่ากลับเท่าเดิม; tooltip source ถูก; อาวุธไม่ตรงถูกปฏิเสธ;
builder deterministic (ไม่ขึ้นกับลำดับ dictionary); mastery ข้ามหลายขั้นในครั้งเดียวถูกต้อง
DoD: ≥ 35 เคส, coverage Character ≥ 85%, ไม่มี UnityEngine

## Stage 06: Grid และ Targeting Domain
ทำ Stage 06 เท่านั้น: กริดและการเล็งเป้า (pure, zero-alloc)
งาน:
1. ArenaGrid จาก arena_map mask; occupancy; GridCoord ↔ index
2. Reachability: BFS 4 ทิศ จำกัด MoveRange, เดินผ่านช่องมีคนไม่ได้, ปลายทางต้องว่าง; คืน path สำหรับเดินทีละช่อง
3. JumpTargets: ระยะ Manhattan ≤ JumpRange, ลงช่องว่างที่ walkable, ข้ามคนได้
4. AreaShapes: Single/Line/Cross/Diamond/Square/Cone ตามทิศจากผู้ใช้ → tile set; ตัดขอบ grid
5. Facing: ทิศหลังเดิน, ทิศหันเข้าหาเป้า; Relative angle → Front/Flank/Back
6. buffer ทั้งหมดเป็น Span ของผู้เรียก ห้าม allocate
Tests: property test 10k ครั้ง (path ยาว ≤ range, ไม่ทะลุสิ่งกีดขวาง), shape ทุกแบบทั้ง 4 ทิศ (golden),
backstab/flank ถูกต้องทุกทิศ, benchmark query < 0.05 ms

## Stage 07: Arena Simulation Core
ทำ Stage 07 เท่านั้น: ArenaSimulation implement IArenaSession / IArenaSessionFactory / IArenaAutoResolver
งาน:
1. struct array ของนักสู้ (ไม่ใช้ ECS ตาม ADR 0002), ใช้ ArenaFrameBuffer + EventRing จาก Stage 02
2. ลำดับต่อ tick: Intro → ATB fill (สกัดจุด/ชะงักมีผล) → Ready/AwaitingPlayer → AI decide (ใช้ hook ไปก่อน, Stage 08 ทำจริง)
   → action progress (Move ทีละช่อง, Jump พร้อม i-frame, WindUp → Resolve → Recovery) → status tick → neili regen
   → defeat/timeout check
3. Resolve: hit/evade (แม่นยำ vs หลบ), crit, backstab/flank, mitigation, counter, knockback (ชนขอบ/คนแล้วหยุด), status
4. Jump ระหว่างอีกฝ่าย WindUp: ถ้าลงนอก area ถือว่าหลบได้ (ตัดสินตอน Resolve)
5. บันทึกสถิติ FighterMatchStat + SkillUseStat
6. AutoResolver: สร้าง session ทั้งสองฝั่งเป็น AI, Step จนจบหรือ maxTicks
7. Sim/ CLI: tools/sim.ps1 --matches 500 → artifacts/balance.csv (winrate ตามระดับฝีมือ, เวลาแมตช์, วิชาที่ใช้)
ห้าม: แก้ contract test เพื่อให้ผ่าน (ถ้าสัญญาผิดให้เขียน ADR แล้วแก้สัญญาพร้อม Fake)
Tests: ArenaSessionContractTests ชุดเดิมผ่าน + เคสดาเมจ golden, counter, knockback, timeout, i-frame
DoD: auto-resolve 1 แมตช์เฉลี่ย < 20 ms บน .NET; GC alloc ต่อ tick = 0 หลัง warm-up

## Stage 08: Arena AI
ทำ Stage 08 เท่านั้น: AI คู่ต่อสู้
งาน:
1. Utility AI ตัดสินใจเมื่อ Ready + รอ AiReactionTicks (จากค่าเด็ดขาด + ระดับความยาก):
   ให้คะแนน UseSkill(ทุก slot × เป้าที่เป็นไปได้) / Move (เข้าหา/ถอย/อ้อมหลัง) / Jump (หลบ telegraph) / Meditate
2. archetype 4 แบบจาก ai_profile (น้ำหนักคะแนน): ประชิด, ยิงแล้วถอย, ถึกภายใน, สายกระโดดหลบ
3. AI ใช้ IArenaQuery เดียวกับผู้เล่น (ห้ามโกงอ่าน state ที่ผู้เล่นไม่เห็น นอกจากค่าที่ HUD แสดง)
4. deterministic ผ่าน RNG สาย arena
Tests: AI หลบ telegraph ได้เมื่อมี jump; ไม่สั่งคำสั่งที่ Validate ไม่ผ่าน (0 ครั้งใน 1,000 แมตช์);
archetype แสดงพฤติกรรมต่างกันวัดได้ (ระยะเฉลี่ยจากเป้า, จำนวน jump)
DoD: sim 500 แมตช์: ระดับฝีมือสูงกว่า 2 ขั้นชนะ ≥ 75%; ไม่มีแมตช์ค้าง; บันทึกผลใน balance report

## Stage 09: Arena Presentation (HD-2D Isometric)
ทำ Stage 09 เท่านั้น: ฉาก Arena เล่นได้ใน Web
งาน:
1. SceneArena: ลานไม้ placeholder 3D, กล้อง ortho iso (ตาม ADR Stage 03), mapping grid → world
2. ArenaRunner: fixed-step accumulator + interpolate Prev→Cur, ความเร็ว 1x/2x, pause เมื่อ tab blur
3. FighterView pool: sprite 4 ทิศ (วาด SE/NE แล้ว flipX), animator ตาม FighterState, ความสูงตอนกระโดด, เงา
4. Tile highlight: ช่องเดิน (เขียว), กระโดด (ฟ้า), ระยะวิชา (เหลือง), area preview (แดง), วงเตือน telegraph ของศัตรู
5. Input: S/D/Space/F, ปุ่ม 1–4 เลือกวิชา, เมาส์ hover แล้ว raycast ไปที่ tile, คลิกซ้ายยืนยัน, คลิกขวาวนโหมด;
   แสดงเหตุผลจาก CommandResult (localization)
6. HUD (R3): HP/ATB/ลมปราณ ทั้ง 2 ฝั่ง, ตัวจับเวลา, ชื่อรอบ, กล่องคำสั่ง + cooldown, ตัวเลขดาเมจ pool (ZString + LitMotion),
   cut-in ชื่อวิชาตอน SkillCast ที่มี flag Ultimate
7. พัฒนากับ Fake ก่อน แล้วสลับเป็น sim จริงผ่าน LifetimeScope
Acceptance: เล่น 1 แมตช์ครบทั้งชนะ/แพ้/หมดเวลาใน Web; architecture test ไม่อ้าง Simulation;
ไม่มี Instantiate/Destroy หลัง warm-up; PlayMode: 60 วินาทีไม่มี exception; ≥ 55 FPS ที่ระดับคุณภาพ Med

## Stage 10: Tournament และ Run Flow
ทำ Stage 10 เท่านั้น: ทัวร์นาเมนต์และเรือนพัก (Application + Domain)
งาน:
1. TournamentState: 32 ที่นั่ง, จัดสายตาม PowerScore + RNG สาย tournament, รอบ 32→16→8→4→2
2. ResolveBackgroundMatches ผ่าน IArenaAutoResolver แบ่งงานข้ามหลายเฟรม (Application คืน progress; Presentation ใช้ UniTask)
3. ApplyMatchResult: idempotent (settlement id), StatGrowth จาก SkillUses, ฟื้น HP/ลมปราณเต็ม
4. RewardOffer: เลือก 1 ใน 3 จาก reward_pool ตามรอบ (RNG สาย reward), สร้างครั้งเดียวต่อรอบ
5. RunFlow: Title → CharacterCreate → Bracket → Arena → Rest → ... → Result; แพ้/เสมอ = จบ
6. ReplayLog = ชุดของ ArenaReplay + คำสั่งของ run
Tests: run เต็มด้วย FakeAutoResolver/Fake session จนเป็นแชมป์และแพ้กลางทาง; รับผลซ้ำไม่ได้รางวัลซ้ำ;
seed เดิมได้สายและรางวัลเดิม; replay run ได้ hash เดิม

## Stage 11: หน้าตัวละครและ Loadout UI
ทำ Stage 11 เท่านั้น: CharacterSheet, Loadout, CharacterCreate UI
งาน:
1. CharacterSheet: หมวดค่าสถานะครบ + radar 5 แกน + ระดับฝีมือ + tooltip แหล่งโบนัส (จาก ModifierStack)
   ทักษะชีวิตแสดงสีจางพร้อมป้าย "ใช้ในโหมดท่องยุทธภพ (เร็วๆ นี้)"
2. Loadout: ช่องวิชา 4+1+1, gear 4; ลากหรือคลิกเพื่อใส่; วิชาที่อาวุธไม่ตรงแสดงสถานะล็อกพร้อมเหตุผล;
   preview เทียบค่า (ก่อน→หลัง เขียว/แดง) ก่อนยืนยัน
3. หน้าข้อมูลวิชา: ระดับ, XP, ระยะ/area diagram, ผลขั้น 5/10
4. CharacterCreate: เลือก background, แจกแต้ม (+/−), สุ่มชื่อ, ตรวจผ่าน CharacterCreation ของ Domain
5. Presenter เรียกเฉพาะ Application; รองรับ TH/EN และข้อความยาว
Acceptance: Playwright: สร้างตัวละคร → เปลี่ยนอาวุธ → วิชา disable → ใส่วิชาใหม่ → ค่าที่หน้าจอตรงกับ Core;
ไม่มี GC spike ระหว่างเปิดหน้า

## Stage 12: Vertical Slice
ทำ Stage 12 เท่านั้น: เชื่อมวงจรเต็ม
งาน:
1. Boot → Title → CharacterCreate → Bracket (ดูสาย + progress คู่อื่น) → Arena → Rest (รางวัล + loadout + sheet) → ... → Result
2. scope: Run scope อยู่ตลอด run; Arena scope dispose ทุกครั้ง (IArenaSession, subscription, pool คืน)
3. Result: รอบที่ไปถึง, สถิติรวม, วิชาที่ขึ้นขั้น
4. DevMenu (dev เท่านั้น): ส่งผ่าน ArenaCommand dev ทั้งหมด + ข้ามรอบ + ปรับค่าสถานะ
Acceptance: เล่นจนเป็นแชมป์และแพ้ได้; วน Arena↔Rest 20 รอบไม่มี session/presenter ค้าง และ memory ไม่โต;
release build มี DevCheatsAllowed=false (มีเทสต์ยืนยัน)

## Stage 13: Save, Resume และ Settings
ทำ Stage 13 เท่านั้น
งาน:
1. Protobuf SaveData: schema/config version, CharacterState, TournamentState, reward offer ที่ค้าง, RNG state ทุกสาย, settlement ids
2. Save หลังทุก transaction ที่ Rest/Bracket; ไม่บันทึกกลางแมตช์ (reload กลางแมตช์ = เริ่มแมตช์เดิมใหม่ด้วย seed เดิม)
3. adapter Browser (IndexedDB ตามวิธีที่ Unity 6.3 รองรับจริง ตรวจจากเอกสาร ห้ามเดา) / Desktop; storage ล้มเหลวต้องแจ้งเตือน
4. migration, backup, corrupt handling; Settings: เสียง, ภาษา, ATB mode (Active/Wait), คุณภาพภาพ, ความเร็ว
Tests: round-trip, migration fixture, payload เสีย, เขียนแข่งกัน, reload ไม่สุ่มรางวัล/สายใหม่
Acceptance: Playwright persistence: refresh ที่ Rest แล้ว resume ตรงทุกค่า; refresh กลางแมตช์แล้วเริ่มแมตช์เดิม

## Stage 14: Content, Juice และ UX
ทำ Stage 14 เท่านั้น
งาน:
1. ตรวจเนื้อหาให้ครบตาม GDD; บาลานซ์ด้วย sim (ผู้เล่นค่ากลางกับ bot ควรถึงรอบ 8 ได้ประมาณ 40–60%)
2. Juice: hit-stop สั้น, screen shake (ตั้งค่าได้), cut-in พู่กัน, เสียงฟัน/กระบี่/ฝ่ามือ, ฝน/ใบไม้ร่วง (ระดับ Med ขึ้นไป)
3. Tutorial แมตช์แรก: เดิน → ใช้วิชา → กระโดดหลบวงเตือน → ปรับลมปราณ
4. Sprite pipeline: Aseprite → import preset (point filter, PPU คงที่, pivot เท้า) + validator เช็กชื่อ animation ครบ
5. ตรวจ license asset + attribution; localization ครบทุกข้อความ
Acceptance: ผู้ทดสอบจริง ≥ 1 คนเล่นจบ tutorial เองได้ (บันทึกผล); ไม่มีข้อความฝังในโค้ด

## Stage 15: Test Harness และ CI
ทำ Stage 15 เท่านั้น
งาน:
1. tools/test.ps1: dotnet + EditMode + PlayMode + Web; รายงาน contract suite แยกบรรทัด Fake/ArenaSimulation
   → artifacts/test-summary.md (PASS/FAIL/NOT RUN)
2. WebBridge (dev เท่านั้น): ready, startSeededRun, snapshot (phase/tick/hp/atb/round), submit ArenaCommand,
   advanceTicks, diagnostics; คำสั่ง bypass ตั้งชื่อ test-only
3. Playwright: smoke, arena-input (กด S + คลิกจริงบน canvas), tournament-run (ผ่าน bridge), persistence
4. tools/ci.ps1: gen → validate → verify-generated → test → sim (200 แมตช์, fail เมื่อ winrate หลุดช่วง) → build → web tests
5. release build ห้ามมี bridge (มีเทสต์ตรวจ)
Acceptance: ci.ps1 คืน exit code ≠ 0 เมื่อ check ล้ม; artifacts ครบ; test ไม่ผ่านแค่เพราะเจอ canvas

## Stage 16: Web Release Candidate
ทำ Stage 16 เท่านั้น
งาน:
1. profile load time, frame time, memory, GC ทั้งตอนแมตช์ปกติและตอน resolve คู่เบื้องหลัง; แยกตามระดับคุณภาพ
2. แก้ bottleneck ที่มีหลักฐาน; Addressables เฉพาะเมื่อ initial download เกินงบ
3. link.xml (Protobuf, MessagePack generated, VContainer, R3, Luban), stripping, compression + hosting header,
   tab blur/resize/stale cache
4. clean build จาก toolchain ที่ล็อก, release checklist, known issues
Acceptance: release เปิดได้บน browser ที่ประกาศรองรับ; required tests ผ่าน; performance report มีสเปกเครื่องและ build hash;
ไม่อ้างตัวเลขที่ไม่ได้วัด

## Stage 17 ขึ้นไป: Feature ต่อยอด (prompt เดียว ใช้ซ้ำได้)
ทำ Stage ถัดไปจาก baseline ปัจจุบัน เปลี่ยนเฉพาะ feature ที่ระบุ
Feature: <เช่น แผนที่ยุทธภพ (node map) / ฝึกวิชาที่สำนัก / ทักษะชีวิตมีผล / ต่อสู้ทีม 3v3 /
ภูมิประเทศสูงต่ำ / ระบบกริดแบบย่าม (loadout แบบ B) / sprite 8 ทิศ>
เหตุผล: <หลักฐานจาก playtest หรือ profiling>
ก่อนลงมือ: ระบุผลต่อ Domain/Data/Save/UI/Simulation/Web perf, เสนอ acceptance + regression tests,
ระบุ save/config migration, ห้ามรวม refactor ใหญ่ที่ไม่จำเป็น
ลำดับ: data → gen → validate → Core tests → sim เทียบบาลานซ์ → Presentation → ci.ps1 → เอกสาร
ห้ามเพิ่มกฎเกมฝั่ง Presentation
หลังลงมือ: รายงานผลที่วัดได้จริง PASS/FAIL/NOT RUN

---

### ลำดับที่แนะนำ
1. ทำ ProjectSurvive ต่อจนจบ Stage 08 แล้วติด tag `template-baseline`
2. ProjectJianghu: 00 → 01 → **02 (Contract)** → 03 → 04 → 05 → 06 → 07 → 08 ทั้งหมดนี้ทดสอบบน .NET ได้โดยไม่ต้องมีภาพ จบ Stage 08 จะได้บอทที่ตีกันเองเป็นทัวร์นาเมนต์ได้แล้ว
3. Stage 09–12 จะได้ prototype ที่เล่นได้บนเว็บ

งานที่ใช้เวลานานที่สุดคือ **ภาพ sprite** ผมแนะนำให้เริ่มจ้างหรือวาดตัวละคร 2 ทิศ × 7 ท่าตั้งแต่ช่วง Stage 05–07 จะได้ทำคู่ขนานกับงานโค้ด