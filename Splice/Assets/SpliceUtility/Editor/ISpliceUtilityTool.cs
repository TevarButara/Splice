namespace Splice.EditorTools
{
    /// <summary>
    /// เครื่องมือ 1 ตัวใน Splice Utility window. เพิ่ม tool ใหม่ = สร้าง class ที่ implement interface นี้
    /// (มี constructor ว่าง) แล้วมันจะโผล่ใน sidebar เองอัตโนมัติ (ค้นด้วย TypeCache — ไม่ต้องไป register มือ)
    /// </summary>
    public interface ISpliceUtilityTool
    {
        /// ชื่อที่โชว์ใน sidebar/แท็บ
        string Title { get; }

        /// ลำดับการเรียงใน sidebar (น้อย = บน)
        int Order { get; }

        /// เรียกตอน tool ถูกเลือก/หน้าต่างเปิด
        void OnEnable();

        /// เรียกตอนสลับไป tool อื่น/หน้าต่างปิด — เก็บกวาด resource ที่นี่
        void OnDisable();

        /// วาด UI ของ tool (อยู่ในพื้นที่ content ด้านขวา, ใน scroll view)
        void OnGUI();
    }
}
