using libsignalservice.push;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Signal_UWP.Storage
{
    public class WhisperTrustStore : TrustStore
    {
        public Stream getKeyStoreInputStream()
        {
            return new FileStream("WhisperTrustStore", FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }

        public string getKeyStorePassword()
        {
            return "whisper";
        }
    }
}
