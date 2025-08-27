using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsingDLL
{
    internal class SharedMemoryImplementation : ISharedMemory
    {
        public int Size { get; }
        public string SharedMemoryName { get; }
        public MemoryMappedFile mmf { get; }

        public SharedMemoryImplementation(int Size = 65535, string SharedMemoryName = "DefaultName")
        {
            this.Size = Size;
            this.SharedMemoryName = SharedMemoryName;
            this.mmf = MemoryMappedFile.CreateOrOpen(SharedMemoryName, Size);
        }

        public void Read(int offset, byte[] bytes, int bytesOffset, int length)
        {
            using (var accessor = mmf.CreateViewAccessor())
            {
                accessor.ReadArray(offset, bytes, bytesOffset, length);
            }
        }

        public int ReadInt(int offset)
        {
            using (var accessor = mmf.CreateViewAccessor())
            {
                return accessor.ReadInt32(offset);
            }
        }

        public void Write(int offset, byte[] bytes, int bytesOffset, int length)
        {
            using (var accessor = mmf.CreateViewAccessor())
            {
                accessor.WriteArray(offset, bytes, bytesOffset, length);
            }
        }

        public void WriteInt(int offset, int value)
        {
            using (var accessor = mmf.CreateViewAccessor())
            {
                accessor.Write(offset, value);
            }
        }
    }
}
