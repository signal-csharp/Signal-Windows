using libsignalservice.push;
using System.IO;

namespace Signal_Windows.Storage
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