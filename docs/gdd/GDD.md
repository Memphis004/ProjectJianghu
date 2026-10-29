# Wuxia: Jianghu Rising — GDD v0.1 และ ArenaBattle Contract v1

เอกสารนี้เป็นข้อกำหนดของเกม (ไม่ใช่ของ template) — แผนงานแยกอยู่ที่ [`../stages/plan.md`](../stages/plan.md) และคำศัพท์ไทย↔อังกฤษอยู่ที่ [`../../llmwiki/glossary.md`](../../llmwiki/glossary.md)

ก่อนเริ่ม มีการตัดสินใจที่ล็อกไว้ 3 ข้อ (สองข้อแรกและข้อสามมี ADR ของเกมกำกับ):
- **Loadout ใช้แบบ A (ช่องสวมใส่):** เกมเน้นค่าสถานะเยอะแบบ 江湖十一 ระบบ build จึงควรมาจาก "วิชา + ระดับความชำนาญ + อาวุธ" ส่วนระบบกริดแบบย่ามเก็บไว้เป็น feature ในอนาคต
- **ไม่ใช้ EcsLite:** ในสนามมีนักสู้ไม่เกิน 8 ตัว ใช้ struct array ธรรมดาก็พอ ECS จะเพิ่มความซับซ้อนโดยไม่ได้อะไรกลับมา — [ADR 0101](../adr/0101-no-ecs-struct-array-fighters.md)
- **ตัวละครหันได้ 4 ทิศแนวทแยงบน iso grid:** การเดินเป็นแบบ 4 ทิศ ตัวละครจึงหันแค่ NE/SE/SW/NW วาดจริงแค่ 2 ชุด (หันหน้ากับหันหลัง) แล้วกลับด้านภาพเอา งานภาพลดลงจากแบบ 8 ทิศมาก — [ADR 0102](../adr/0102-four-facing-sprites-two-drawn-mirrored.md)

เรื่องลิขสิทธิ์: กลไกเกมลอกได้ แต่ชื่อวิชา ข้อความ ภาพ และ UI layout ต้องทำขึ้นใหม่ ห้ามเอาของจาก 江湖十一, Wandering Sword หรือเกมต้นแบบมาใช้ตรงๆ

---

## ส่วนที่ 1: GDD ฉบับย่อ (v0.1)

### 1.1 ภาพรวม

| หัวข้อ | รายละเอียด |
|---|---|
| ชื่อเกม | **Wuxia: Jianghu Rising** (ยุทธภพผงาด) / codename `ProjectJianghu` |
| ชื่อสำรอง | Wuxia Ascendant, Wuxia: Ten Thousand Blades |
| แนว | Wuxia RPG ต่อสู้แบบ ATB บนกริด isometric (อนาคตเพิ่ม Open World Sandbox) |
| Prototype | โหมด **ทัวร์นาเมนต์ลานประลอง** ผู้เข้าแข่ง 32 คน ตีทีละคู่จนได้แชมป์ |
| ภาพ | HD-2D pixel art แบบ Wandering Sword / Octopath แต่กล้องเป็น isometric แบบ wuxia-arena |
| Platform แรก | Unity 6.3 → WebGL |

**Elevator pitch:** สร้างจอมยุทธ์ที่มีค่าสถานะลึกระดับ 江湖十一 เลือกวิชาและอาวุธมาจัด build แล้วขึ้นลานประลองที่หลอด ATB เดินตลอดเวลา คู่ต่อสู้ไม่รอให้คุณคิด ต้องตัดสินใจทุกจังหวะว่าจะเดิน ใช้วิชา หรือกระโดดหลบ

### 1.2 Pillars
- **ค่าสถานะบอกตัวตน:** ตัวเลขทุกค่ามีผลในการต่อสู้จริง และอ่านเข้าใจได้จากหน้าตัวละคร
- **ATB ที่กดดัน:** ใช้โหมด Active เป็นค่าเริ่มต้น ศัตรูไม่หยุดรอ ส่วนโหมด Wait มีไว้เป็นตัวเลือกเพื่อการเข้าถึง
- **Build จากวิชาและอาวุธ:** วิชาต้องเข้าคู่กับอาวุธที่ถือ และวิชาภายในกับวิชาตัวเบาเปลี่ยนสไตล์การเล่นได้จริง
- **ยิ่งใช้ยิ่งเก่ง:** วิชาที่ใช้บ่อยจะขึ้นระดับและให้ค่าสถานะเพิ่ม เป็นเมล็ดพันธุ์ของระบบ sandbox ในอนาคต

### 1.3 Core Loop ของ Prototype

```text
[สร้างตัวละคร] เลือกภูมิหลัง 1 ใน 4 → แจกแต้มค่าสถานะ → เลือกวิชาเริ่มต้น
   ↓
[ผังทัวร์นาเมนต์ 32 คน] เห็นสายการแข่ง และคู่อื่นแข่งเบื้องหลังแบบ auto-resolve
   ↓
┌→ [ลานประลอง] ATB 1 ต่อ 1 → ชนะ / แพ้ / หมดเวลา
│     ↓ ชนะ
│  [เรือนพัก] ฟื้น HP และลมปราณเต็ม → วิชาที่ใช้ได้ค่าชำนาญ → เลือกรางวัล 1 ใน 3
│     → จัด loadout → ดูหน้าค่าสถานะ
└── [รอบถัดไป] 32 → 16 → 8 → 4 → 2 → แชมป์
   ↓ แพ้ = จบรอบ
[Result] รอบที่ไปถึง, สถิติ, ชื่อเสียง (เก็บไว้ใช้กับ meta ในอนาคต)
```

### 1.4 ระบบค่าสถานะ (แรงบันดาลใจจาก 江湖十一 แต่ชื่อและสูตรทำใหม่)

กราฟ radar 5 แกน: **วรยุทธ์, ลมปราณ, กายา, จิตใจ, เส้นปราณ**

| หมวด | ค่าย่อย | ใน Prototype |
|---|---|---|
| วรยุทธ์ (ความชำนาญอาวุธ) | หมัดฝ่ามือ, กระบี่, ดาบ, ดัชนี, ทวน, กระบอง, อาวุธลับ | ใช้ 4 ค่า: หมัดฝ่ามือ, กระบี่, ดาบ, อาวุธลับ |
| ลมปราณ | ปริมาณปราณ, หมุนเวียนปราณ, พลังปราณ, กายเบา | ครบ |
| กายา | กำลังเอว, ช่วงล่าง, กายแกร่ง, เอ็นกระดูก | ครบ |
| จิตใจ | มุ่งมั่น, สมาธิ, สุขุม, เด็ดขาด, ปฏิภาณ | ครบ |
| เส้นปราณ | ชีพจรตู, ชีพจรเริน | ครบ |
| ทักษะชีวิต | ปัญญา, ความรู้, คุณธรรม, เสน่ห์, วาสนา, การแพทย์, พิษวิทยา, ทำอาหาร, สุรา, ดนตรี, พู่กัน, ใจสังหาร | แสดงในหน้าตัวละครแต่ยังไม่มีผล (เตรียมไว้สำหรับ sandbox) ยกเว้น **ปัญญา** ที่ใช้เพิ่มความเร็วฝึกวิชา |

**ค่าต่อสู้ที่คำนวณได้ (Derived Stats):** สัมประสิทธิ์ทุกตัวอยู่ในตาราง `stat_formula.csv` ไม่ฝังในโค้ด

| ค่าต่อสู้ | คำนวณจาก | ตัวอย่างสูตรเริ่มต้น |
|---|---|---|
| HP สูงสุด | กายแกร่ง, เอ็นกระดูก | 30,000 + กายแกร่ง×600 + เอ็นกระดูก×200 (ตั้งเป้าให้อยู่ราว 80k–120k) |
| ลมปราณสูงสุด | ปริมาณปราณ, ชีพจรตู | 1,000 + ปริมาณ×40 + ตู×20 |
| ฟื้นปราณ/วินาที | หมุนเวียนปราณ, ชีพจรเริน | |
| พลังภายนอก | ความชำนาญอาวุธที่ถือ + กำลังเอว | |
| พลังภายใน | พลังปราณ | |
| ป้องกัน / ป้องกันภายใน | เอ็นกระดูก, ช่วงล่าง / ปริมาณปราณ, สุขุม | ลดดาเมจด้วยสูตร Def / (Def + K) |
| ระยะเดิน / ระยะกระโดด | กายเบา | 3 + floor(กายเบา / 30) |
| เวลาเติม ATB | กายเบา, เด็ดขาด | หน่วยเป็น tick เติมเต็มได้เร็วสุดตามค่า constant |
| แม่นยำ / หลบ | สมาธิ / กายเบา | |
| คริติคอล / ดาเมจคริ | ปฏิภาณ / กำลังเอว | |
| ต้านสถานะ / ต้านผลัก | มุ่งมั่น, ชีพจรเริน / ช่วงล่าง | |
| สวนกลับ | สุขุม | |

**ค่าภาพรวม:** มี "คะแนนยุทธ์" (power score แบบถ่วงน้ำหนัก) และ "ระดับฝีมือ" 9 ขั้น ใช้แสดงผลและจัดสายทัวร์นาเมนต์

**สูตรดาเมจ (ค่าทุกตัวมาจาก data):**

$$\text{Dmg} = (\text{Base} + \text{Power}\times\text{Ratio}) \times \text{Mastery} \times (1-\text{Mitigation}) \times \text{Crit} \times \text{Backstab} \times \text{Var}_{0.95\text{–}1.05}$$

### 1.5 วิชาและอาวุธ (Loadout แบบ A)

| ช่อง | จำนวน | บทบาท |
|---|---|---|
| วิชาภายนอก | 4 | สกิลที่กด D ใช้ได้ ต้องตรงกับประเภทอาวุธที่ถือ (วิชาหมัดใช้ได้เฉพาะตอนมือเปล่าหรือสวมถุงมือ) |
| วิชาภายใน | 1 | passive เพิ่มค่าสถานะ และกำหนดธาตุของพลังภายใน |
| วิชาตัวเบา | 1 | เพิ่มระยะเดินและระยะกระโดด, ATB, การหลบ และให้ผลพิเศษตอนกระโดด |
| อาวุธ | 1 | กำหนดประเภทวิชาภายนอกที่ใช้ได้ และให้พลังพื้นฐาน |
| ชุด | 1 | ป้องกันและ HP |
| เครื่องประดับ | 2 | ค่าสถานะย่อยและผลพิเศษ |

**ระดับวิชา 1–10 ขั้น:** วิชาที่เรียนแล้วให้โบนัสค่าสถานะ เช่น "ฝ่ามือคลื่นซัด ขั้น 5: หมัดฝ่ามือ +5" เมื่อถึงขั้น 5 และ 10 จะปลดล็อกผลพิเศษ วิชาที่ใช้ในแมตช์จะได้ค่าชำนาญหลังจบแมตช์ และค่า **ปัญญา** ช่วยให้ได้ค่าชำนาญเร็วขึ้น

**ตัวอย่างเนื้อหา Prototype (ชื่อตั้งใหม่ทั้งหมด):**

| ประเภท | ตัวอย่าง |
|---|---|
| กระบี่ | กระบี่สายฝนพรำ (ระยะ 1, ฟัน 2 ครั้ง), กระบี่จันทร์เสี้ยว (แนวเส้น 3 ช่อง) |
| ดาบ | ดาบพยัคฆ์คำราม (รูปกรวย, ผลักถอย), ดาบผ่าภูผา (ดาเมจสูง, wind-up นาน) |
| หมัดฝ่ามือ | ฝ่ามือคลื่นซัด (พลังภายใน, ทำให้บาดเจ็บภายใน), หมัดหินผา (มีโอกาสสกัดจุด) |
| อาวุธลับ | เข็มเมฆาบิน (ระยะ 2–5, ติดพิษ), ลูกดอกสามดาว (กระจาย 3 เป้า) |
| วิชาภายใน | คัมภีร์ปราณหยาง (พลังภายใน +), จิตวารี (ฟื้นปราณ +), กายวัชระ (ป้องกัน +) |
| วิชาตัวเบา | เหินลมเหยียบใบไผ่ (ระยะกระโดด +2), เงาพริ้วไหว (หลบ +, ATB เร็วขึ้น) |
| สถานะผิดปกติ | สกัดจุด (ATB หยุด), บาดเจ็บภายใน (ฟื้นปราณช้าลง + DOT), พิษ (DOT), ชะงัก (ATB ช้าลง) |

### 1.6 ระบบต่อสู้ ATB

- **สนาม:** กริด 12×12 (ขนาดมาจาก data) เดินได้ 4 ทิศแบบ Manhattan ช่องที่อยู่ในระยะเดินเป็นไฮไลต์สีเขียว
- **ATB:** หลอดเติมตามเวลา เมื่อเต็มแล้วผู้เล่นสั่งได้ 1 คำสั่ง
  - **Active (ค่าเริ่มต้น):** ฝ่ายตรงข้ามไม่รอ
  - **Wait:** เกมหยุดระหว่างที่ผู้เล่นเลือกคำสั่ง
  - เกมหยุดเองเมื่อแท็บเบราว์เซอร์ไม่ได้ active
- **คำสั่ง:**

| ปุ่ม | คำสั่ง | ค่า ATB ที่ใช้ |
|---|---|---|
| S | เดิน (เลือกช่องในระยะ) | 100% |
| D | ใช้วิชา (เลือกช่อง 1–4 แล้วเลือกเป้า มี preview พื้นที่โดน) | 100% (บางวิชาคืน ATB บางส่วนได้) |
| Space | กระโดด: ข้ามช่องที่มีคนยืนได้ และอยู่ในสถานะ i-frame ระหว่างลอยตัว มี cooldown | ประมาณ 50% (ค่าใน data) |
| F | ปรับลมปราณ: ฟื้นลมปราณทันที | 100% |
| คลิกซ้าย | ยืนยันคำสั่ง | |
| คลิกขวา | เปลี่ยนโหมดคำสั่ง (เป็นแค่ UI ไม่ส่งเข้า sim) | |

- **จังหวะของแต่ละคำสั่ง:** wind-up → ผลเกิด → recovery ระหว่าง wind-up ศัตรูจะเห็นวงเตือนบนพื้น จึงกระโดดหลบได้
- **ตำแหน่งมีผล:** ตีจากด้านข้างหรือด้านหลังได้โบนัสคริ และมีโอกาสโดนสวนกลับเมื่อตีจากด้านหน้า
- **เวลาแมตช์ 120 วินาที:** หมดเวลาแล้วฝ่ายที่มี HP% สูงกว่าชนะ ถ้าเท่ากันถือว่าเสมอ และในทัวร์นาเมนต์เสมอนับเป็นแพ้ของผู้เล่น
- **AI:** มี 4 archetype คือ ประชิดตัว / ยิงระยะไกลแล้วถอย / ถึกพลังภายใน / สายกระโดดหลบ ความเร็วในการตัดสินใจมาจากค่า **เด็ดขาด** ของ NPC บวกระดับความยาก

### 1.7 ทัวร์นาเมนต์
- มี NPC 31 คนจาก `fighter.csv` แต่ละคนมีค่าสถานะ loadout และ AI ของตัวเอง ผู้เล่นคือคนที่ 32
- จัดสายตามคะแนนยุทธ์ ผู้เล่นเริ่มเป็นมวยรอง
- คู่ที่ผู้เล่นไม่ได้แข่งจะ **auto-resolve ด้วย sim ตัวจริงแบบ headless** ให้ทั้งสองฝั่งใช้ AI แล้วแบ่งงานข้ามหลายเฟรมเพื่อไม่ให้เกมกระตุก
- รางวัลระหว่างรอบเลือก 1 ใน 3: คัมภีร์วิชาใหม่ / อาวุธหรือของสวมใส่ / ยาเม็ดเพิ่มค่าสถานะถาวร / ฝึกวิชา (เพิ่มค่าชำนาญ)

### 1.8 ภาพและ UI
- **HD-2D isometric:** ฉากเป็น 3D low-poly ใช้ texture แบบ pixel (point filter) วางตัวละคร pixel sprite แบบ billboard ใช้กล้อง **orthographic** หมุน 45° ก้มประมาณ 30–35° ใช้แสง URP, ฝน/ใบไม้ร่วงด้วย particle ส่วน bloom และ DoF แบบ tilt-shift **แยกเป็นระดับคุณภาพ** เพราะหนักสำหรับ WebGL
- **Sprite:** เฟรมประมาณ 64×80 px วาด 2 ทิศแล้วกลับด้าน มีท่า idle 4, walk 6, attack 6, cast 4, jump 4, hit 2, death 4 เฟรม
- **Skill cut-in:** แบนเนอร์ชื่อวิชาแบบพู่กันจีนตอนใช้วิชาใหญ่ (ได้แรงบันดาลใจจากภาพที่ 1)
- **HUD:** หลอด HP และ ATB ของทั้งสองฝั่งด้านบน, ตัวจับเวลา, รอบการแข่ง, กล่องคำสั่งมุมซ้ายล่าง, หลอดลมปราณ
- **หน้าตัวละคร:** มีกราฟ radar ค่าสถานะแบ่งหมวด และ tooltip บอกที่มาของโบนัส เช่น "+3 จากฝ่ามือคลื่นซัด"

### 1.9 ขอบเขต
- **มีใน Prototype:** สร้างตัวละคร, ทัวร์นาเมนต์ 32 คน, สนาม 1 แผนที่, วิชาประมาณ 13 แบบ, ของสวมใส่ประมาณ 12 ชิ้น, NPC 31 คน, save/resume, TH/EN
- **อนาคต (Sandbox):** แผนที่โลกแบบจุดเชื่อมโยง (สำนัก/เมือง/ลานประลอง), เวลาเป็นวัน, ฝึกวิชาจากสำนัก, ทักษะชีวิต, ความสัมพันธ์, การต่อสู้เป็นทีม (contract รองรับนักสู้ได้ถึง 8 ตัวไว้แล้ว), ภูมิประเทศสูงต่ำ
- **ตอนนี้ต้องวางทางไว้ให้ sandbox แล้ว:** ทำ `StatGrowth` เป็น API กลาง และให้ `ArenaMatchResult.SkillUses` ส่งค่าชำนาญออกมาได้

---

## ส่วนที่ 2: ArenaBattle Contract v1

ไฟล์อยู่ที่ `lib/Runtime/Domain/Arena/` (namespace `ProjectJianghu.Core.Domain.Arena`) ห้ามอ้าง UnityEngine, R3, VContainer และ UniTask

แพทเทิร์นยังเหมือน `IBattleSession` เดิม คือ sim เดิน fixed tick 30Hz เพราะ ATB เป็นระบบที่ขึ้นกับเวลา มีจุดที่ต่างกันดังนี้:
- `Step()` ไม่รับ input แล้ว
- การกระทำทั้งหมดส่งผ่าน `Submit(ArenaCommand)`
- มี `IArenaQuery` ไว้คำนวณไฮไลต์ช่องโดยไม่แก้ state

**`ArenaConstants.cs` / `ArenaEnums.cs`**
```csharp
namespace ProjectJianghu.Core.Domain.Arena
{
    public static class ArenaConstants
    {
        public const int TickRate = 30;
        public const float TickDelta = 1f / TickRate;
        public const int MaxStepsPerFrame = 4;
        public const int AtbFull = 10_000;          // ATB เป็น fixed-point
        public const int MaxFighters = 8;           // prototype ใช้ 2 แต่เผื่อทีมในอนาคต
        public const int MaxSkillSlots = 4;
        public const int MaxGridSize = 32;
        public const int MaxGridTiles = MaxGridSize * MaxGridSize;
        public const int EventRingCapacity = 2048;
        public const byte NoFighter = 255;
    }

    public enum ArenaPhase : byte
    {
        Intro = 0,           // นับถอยหลังก่อนเริ่ม ATB ยังไม่เดิน
        Running = 1,
        AwaitingPlayer = 2,  // ใช้เฉพาะ AtbMode.Wait: sim หยุดจนผู้เล่นสั่ง
        Finished = 3,
    }

    public enum AtbMode : byte { Active = 0, Wait = 1 }

    public enum FighterState : byte
    {
        Charging, Ready, Moving, Jumping, WindUp, Recovery, Stunned, Defeated,
    }

    public enum IsoDir : byte { NE = 0, SE = 1, SW = 2, NW = 3 }   // ภาพวาด SE/NE แล้วกลับด้านเป็น SW/NW
}
```

**`GridCoord.cs`**
```csharp
using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly short X, Y;
        public GridCoord(int x, int y) { X = (short)x; Y = (short)y; }

        public static readonly GridCoord Invalid = new GridCoord(-1, -1);
        public bool IsValid => X >= 0 && Y >= 0;
        public int Manhattan(GridCoord o) => Math.Abs(X - o.X) + Math.Abs(Y - o.Y);

        public bool Equals(GridCoord o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is GridCoord o && Equals(o);
        public override int GetHashCode() => (X << 16) ^ (ushort)Y;
        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
    }
}
```

**`ArenaCommand.cs`**
```csharp
namespace ProjectJianghu.Core.Domain.Arena
{
    public enum ArenaCommandKind : byte
    {
        Move = 0,         // Target = ช่องปลายทาง
        UseSkill = 1,     // Slot = 0..3, Target = ช่องเป้า
        Jump = 2,         // Target = ช่องลงจอด
        Meditate = 3,     // ปรับลมปราณ
        Surrender = 10,
        DevKillEnemies = 100, DevGodMode = 101, DevFillAtb = 102, DevWinNow = 103, DevLoseNow = 104,
    }

    public readonly struct ArenaCommand
    {
        public readonly ArenaCommandKind Kind;
        public readonly byte Actor;
        public readonly byte Slot;
        public readonly GridCoord Target;
        public readonly int Arg;

        public ArenaCommand(ArenaCommandKind kind, byte actor, GridCoord target, byte slot = 0, int arg = 0)
        { Kind = kind; Actor = actor; Target = target; Slot = slot; Arg = arg; }

        public static ArenaCommand Move(byte a, GridCoord t) => new ArenaCommand(ArenaCommandKind.Move, a, t);
        public static ArenaCommand Skill(byte a, byte slot, GridCoord t) => new ArenaCommand(ArenaCommandKind.UseSkill, a, t, slot);
        public static ArenaCommand Jump(byte a, GridCoord t) => new ArenaCommand(ArenaCommandKind.Jump, a, t);
        public static ArenaCommand Meditate(byte a) => new ArenaCommand(ArenaCommandKind.Meditate, a, GridCoord.Invalid);
    }

    public enum CommandResult : byte
    {
        Ok = 0,
        RejectedWrongPhase,
        RejectedNotReady,       // ATB ยังไม่เต็ม หรือกำลังทำ action อื่นอยู่
        RejectedNotControlled,  // สั่งนักสู้ที่ AI คุมอยู่
        RejectedOutOfRange,
        RejectedBlocked,        // ช่องมีคนอยู่ / เดินไม่ได้ / ไม่มีเส้นทาง
        RejectedNoNeili,
        RejectedOnCooldown,
        RejectedWeaponMismatch,
        RejectedInvalidArg,
        RejectedNotAllowed,     // dev command ใน release
    }
}
```

**`ArenaEvent.cs`**
```csharp
namespace ProjectJianghu.Core.Domain.Arena
{
    public enum ArenaEventType : ushort
    {
        MatchIntro, MatchStarted, AtbReady, CommandAccepted,
        MoveStarted, MoveEnded, JumpStarted, Landed,
        SkillWindUp,      // Tile = จุดศูนย์กลาง; ใช้วาดวงเตือนจาก IArenaQuery.PreviewSkillArea
        SkillCast,        // ใช้แสดง cut-in
        Hit, Evaded, Countered, KnockedBack,
        StatusApplied, StatusExpired, NeiliRecovered,
        FighterDefeated, TimeWarning, MatchEnded,   // MatchEnded: Value = ArenaEndReason
    }

    [System.Flags]
    public enum ArenaEventFlags : byte { None = 0, Critical = 1, Backstab = 2, Flank = 4, Inner = 8, Ultimate = 16 }

    public struct ArenaEvent
    {
        public uint Seq;
        public int Tick;
        public ArenaEventType Type;
        public ArenaEventFlags Flags;
        public byte Source, Target;   // fighter id หรือ NoFighter
        public int DefId;             // skill / status id
        public int Value;             // ดาเมจ, ฮีล, ระยะเวลา, reason
        public int Value2;            // HP หลังโดน ฯลฯ ดูได้ใน arena-contract.md
        public GridCoord Tile;
    }

    /// ใช้ EventRing<T> ตัวกลางที่แยกออกมาจาก BattleEventRing ของ template (ADR)
    public interface IArenaEventSource
    {
        uint LatestSeq { get; }
        int Read(ref uint cursor, System.Span<ArenaEvent> destination, out int dropped);
    }
}
```

**`ArenaFrame.cs`**
```csharp
using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    public struct FighterView
    {
        public byte Id, Team;
        public int DefId;                 // appearance / roster id
        public FighterState State;
        public IsoDir Facing;
        public GridCoord Tile;
        public float PrevX, PrevY, X, Y;  // พิกัดต่อเนื่องบนกริด ใช้ interpolate ระหว่างเดิน/กระโดด
        public float Height;              // ความสูงตอนกระโดด
        public int Hp, MaxHp, Neili, MaxNeili;
        public int Atb;                   // 0..AtbFull
        public ushort StatusMask;
        public int ActiveSkillId;         // ระหว่าง WindUp/Recovery
        public ushort ActionProgressPermille;
        public ushort JumpCooldownTicks;
    }

    /// span ใช้ได้จนกว่าจะเรียก Step/Submit ครั้งถัดไป
    public interface IArenaFrame
    {
        int Tick { get; }
        int ElapsedTicks { get; }
        int TimeLimitTicks { get; }
        byte PlayerFighter { get; }
        ReadOnlySpan<FighterView> Fighters { get; }
        ReadOnlySpan<ushort> SkillCooldowns(byte fighter);   // ยาว MaxSkillSlots
    }

    /// query บริสุทธิ์ ไม่แก้ state เขียนผลลง buffer ของผู้เรียก (zero-alloc)
    public interface IArenaQuery
    {
        int GetReachableTiles(byte fighter, Span<GridCoord> dest);
        int GetJumpTiles(byte fighter, Span<GridCoord> dest);
        int GetSkillRangeTiles(byte fighter, byte slot, Span<GridCoord> dest);
        int PreviewSkillArea(byte fighter, byte slot, GridCoord target, Span<GridCoord> dest);
        /// ต้องให้ผลตรงกับ Submit ทุกกรณี (มี contract test ตรวจ)
        CommandResult Validate(in ArenaCommand command);
        bool IsWalkable(GridCoord tile);
        int Width { get; }
        int Height { get; }
    }
}
```

**`ArenaSetup.cs`**
```csharp
using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// สร้างโดย Domain.Character (StatResolver) แล้ว Arena ห้ามคำนวณจาก attribute ซ้ำ
    public struct CombatStatBlock
    {
        public int MaxHp, MaxNeili, NeiliRegenPerSecX100;
        public int ExternalPower, InnerPower, Defense, InnerDefense;
        public int AccuracyX100, EvasionX100, CritChanceX100, CritDamageX100;
        public int CounterChanceX100, StatusResistX100, KnockbackResist;
        public int AtbFillTicks;            // เวลาเติม ATB 0→เต็ม
        public int ActionSpeedX100;         // คูณกับ wind-up/recovery
        public byte MoveRange, JumpRange;
        public int JumpCooldownTicks;
    }

    public enum SkillTargeting : byte { Enemy, Ground, Self }
    public enum AreaShape : byte { Single, Line, Cross, Diamond, Square, Cone }
    public enum DamageKind : byte { External, Inner }

    [System.Flags]
    public enum SkillFlags : ushort { None = 0, Pierce = 1, IgnoreGuard = 2, Uncounterable = 4, Ultimate = 8, AtbRefund = 16 }

    public struct ArenaSkillLoadout
    {
        public int SkillId;
        public byte Slot, Level;                 // ระดับ 1..10
        public byte WeaponType;                  // ตรงกับ enum ของ Luban
        public SkillTargeting Targeting;
        public byte RangeMin, RangeMax;
        public AreaShape Area; public byte AreaSize;
        public DamageKind Kind;
        public int BaseDamage, RatioX100, MasteryX100;
        public int Hits;
        public int WindUpTicks, RecoveryTicks, CooldownTicks;
        public int NeiliCost, AtbRefundPermille;
        public int StatusId, StatusChanceX100, StatusDurationTicks;
        public byte KnockbackTiles;
        public SkillFlags Flags;
    }

    public sealed class ArenaFighterSetup
    {
        public byte Id; public byte Team; public int DefId;
        public bool PlayerControlled; public int AiProfileId; public int AiReactionTicks;
        public GridCoord StartTile; public IsoDir StartFacing;
        public int StartHp = -1;                 // -1 = เต็ม
        public CombatStatBlock Stats;
        public IReadOnlyList<ArenaSkillLoadout> Skills;
        public IReadOnlyList<int> PassiveIds;    // จากวิชาภายใน/ตัวเบา/เครื่องประดับ
    }

    public sealed class ArenaMatchSetup
    {
        public ulong Seed; public int ArenaMapId; public AtbMode Mode;
        public int TimeLimitTicks;
        public IReadOnlyList<ArenaFighterSetup> Fighters;
        public bool DevCheatsAllowed;
    }
}
```

**`ArenaResult.cs` / `IArenaSession.cs`**
```csharp
using System;
using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Arena
{
    public enum ArenaOutcome : byte { Victory, Defeat, Draw, Aborted }   // มองจากฝั่งผู้เล่น
    public enum ArenaEndReason : byte
    { None, AllEnemiesDefeated, AllAlliesDefeated, TimeoutHpRatio, TimeoutDraw, Surrendered, DevForced }

    public readonly struct SkillUseStat
    {
        public readonly int SkillId, Uses, Hits;
        public SkillUseStat(int skillId, int uses, int hits) { SkillId = skillId; Uses = uses; Hits = hits; }
    }

    public sealed class FighterMatchStat
    {
        public byte Id; public int FinalHp, DamageDealt, DamageTaken, Evades, Counters;
        public IReadOnlyList<SkillUseStat> SkillUses;   // ใช้คำนวณค่าชำนาญ: "ยิ่งใช้ยิ่งเก่ง"
    }

    public sealed class ArenaMatchResult
    {
        public ArenaOutcome Outcome; public ArenaEndReason Reason;
        public byte WinnerTeam;       // 255 = เสมอ
        public int DurationTicks;
        public IReadOnlyList<FighterMatchStat> Fighters;
    }

    public interface IArenaSession : IDisposable
    {
        ArenaPhase Phase { get; }
        int Tick { get; }
        AtbMode Mode { get; }
        void Step();                               // ไม่เดินเมื่อ Finished/AwaitingPlayer
        CommandResult Submit(in ArenaCommand cmd); // atomic และบันทึกลง replay พร้อม Tick
        IArenaFrame Frame { get; }
        IArenaQuery Query { get; }
        IArenaEventSource Events { get; }
        bool TryGetResult(out ArenaMatchResult result);
        ulong ComputeStateHash();
    }

    public interface IArenaSessionFactory { IArenaSession Create(ArenaMatchSetup setup); }

    /// สำหรับคู่เบื้องหลังในทัวร์นาเมนต์: ทั้งสองฝั่งใช้ AI แบบ headless
    public interface IArenaAutoResolver
    {
        ArenaMatchResult Resolve(ArenaMatchSetup setup, int maxTicks);
    }
}
```

**ตัวอย่างฝั่ง Unity (ArenaRunner):**
```csharp
_acc += Time.unscaledDeltaTime * _speed;
int steps = 0;
while (_acc >= ArenaConstants.TickDelta && steps++ < ArenaConstants.MaxStepsPerFrame)
{ _session.Step(); _acc -= ArenaConstants.TickDelta; }
if (steps > ArenaConstants.MaxStepsPerFrame) _acc = 0f;

var me = _session.Frame.PlayerFighter;
if (_commandBox.Mode == CommandMode.Move && IsReady(me))
{
    int n = _session.Query.GetReachableTiles(me, _tileBuf);
    _highlight.Show(_tileBuf.AsSpan(0, n));         // ช่องสีเขียว
}
if (_input.LeftClick && _cursorTile.IsValid)
{
    var r = _session.Submit(BuildCommand(me, _cursorTile));
    if (r != CommandResult.Ok) _commandBox.ShowReject(r);   // ข้อความจาก localization
}
```
