using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoScreenStudio
{
    class UnpackItem
    {
        public ulong pos = 0;
        public ulong length = 0;
        public TimeSpan frameTime = TimeSpan.Zero;
        public Windows.Storage.Streams.Buffer compressedBuffer = null;
    }


}