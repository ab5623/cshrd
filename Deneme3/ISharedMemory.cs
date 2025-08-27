using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsingDLL
{
    internal interface ISharedMemory
    {
        int Size { get; }
        string SharedMemoryName { get; }

        public MemoryMappedFile mmf { get; }

        void Read(int offset, byte[] bytes, int bytesOffset, int length);
        void Write(int offset, byte[] bytes, int bytesOffset, int length);

        int ReadInt(int offset);
        void WriteInt(int offset, int value);


    }
}
