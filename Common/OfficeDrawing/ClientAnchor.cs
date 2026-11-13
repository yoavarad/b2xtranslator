


using System.IO;

namespace b2xtranslator.OfficeDrawing
{
    [OfficeRecord(0xF010)]
    public class ClientAnchor : Record
    {
        public byte[] Bytes;

        public ClientAnchor(BinaryReader _reader, uint size, uint typeCode, uint version, uint instance)
            : base(_reader, size, typeCode, version, instance)
        {
            this.Bytes = this.Reader.ReadBytes((int)this.BodySize);
        }

        //these are only valid for Powerpoint
        // 16-byte form is OfficeArt RectStruct: Int32 left, top, right, bottom (LibreOffice ProcessClientAnchor2)
        public int Top
        {
            get
            {
                if (this.Bytes.Length == 16)
                    return System.BitConverter.ToInt32(this.Bytes, 4);
                return System.BitConverter.ToInt16(this.Bytes, 0);
            }
        }
        public int Left
        {
            get
            {
                if (this.Bytes.Length == 16)
                    return System.BitConverter.ToInt32(this.Bytes, 0);
                return System.BitConverter.ToInt16(this.Bytes, 2);
            }
        }
        public int Right
        {
            get
            {
                if (this.Bytes.Length == 16)
                    return System.BitConverter.ToInt32(this.Bytes, 8);
                return System.BitConverter.ToInt16(this.Bytes, 4);
            }
        }
        public int Bottom
        {
            get
            {
                if (this.Bytes.Length == 16)
                    return System.BitConverter.ToInt32(this.Bytes, 12);
                return System.BitConverter.ToInt16(this.Bytes, 6);
            }
        }
    }

}
