namespace Splice.Core
{
    // บทบาทของผู้เล่น "ต่อ 1 raid" (ไม่ใช่ฝ่ายถาวร) — โมเดลใหม่ (architecture §1.1): ทุกคนมี faction เดียว
    // สร้างเมือง + มอนได้หมด. ในการ raid หนึ่งครั้ง:
    //   Attacker = ฝ่ายที่ยกทัพมอนไปบุก
    //   Defender = เมืองเป้าหมายที่ตั้งรับ (ป้อม+มอนเฝ้า จาก snapshot/AI)
    // ผู้เล่นคนเดียวเป็น Attacker ตอนบุก และเมืองตัวเองเป็น Defender ตอนถูกบุก — บทบาทสลับตามแมตช์ ไม่ใช่ตัวตนถาวร.
    // โหมดทีม (2:1/4:1): เพื่อนร่วมทีมใช้ RaidSide เดียวกัน → N-vs-1 ตกผลึกจากการเป็นสมาชิกฝั่งเดียวกัน ไม่ต้อง special-case รายคน.
    // ลำดับค่าคงเดิม (Attacker=0, Defender=1) — scene/prefab ที่ serialize ค่า Invaders/Defenders เดิมไว้จึงไม่พัง.
    public enum RaidSide
    {
        Attacker,
        Defender
    }
}
