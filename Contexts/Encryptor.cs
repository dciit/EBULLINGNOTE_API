using System.Security.Cryptography;

namespace INVOICE_VENDER_API.Contexts
{
    public class Encryptor
    {
        private EncryptTransformer transformer;

        private byte[] initVec;

        private byte[] encKey;

        public byte[] IV
        {
            get
            {
                return initVec;
            }
            set
            {
                initVec = value;
            }
        }

        public byte[] Key => encKey;

        public Encryptor(EncryptionAlgorithm algId)
        {
            transformer = new EncryptTransformer(algId);
        }

        public byte[] Encrypt(byte[] bytesData, byte[] bytesKey)
        {
            MemoryStream memoryStream = new MemoryStream();
            ICryptoTransform cryptoServiceProvider = transformer.GetCryptoServiceProvider(bytesKey, initVec);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoServiceProvider, CryptoStreamMode.Write);
            try
            {
                cryptoStream.Write(bytesData, 0, bytesData.Length);
            }
            catch (Exception ex)
            {
                throw new Exception("Error while writing encrypted data to the stream: \n" + ex.Message);
            }

            encKey = transformer.Key;
            cryptoStream.FlushFinalBlock();
            cryptoStream.Close();
            return memoryStream.ToArray();
        }
    }

    internal class EncryptTransformer
    {
        private EncryptionAlgorithm algorithmID;

        private byte[] initVec;

        private byte[] encKey;

        internal byte[] IV
        {
            get
            {
                return initVec;
            }
            set
            {
                initVec = value;
            }
        }

        internal byte[] Key => encKey;

        internal EncryptTransformer(EncryptionAlgorithm algId)
        {
            algorithmID = algId;
        }

        public ICryptoTransform GetCryptoServiceProvider(byte[] bytesKey, byte[] initVec)
        {
            switch (algorithmID)
            {
                case EncryptionAlgorithm.Des:
                    {
                        DES dES = new DESCryptoServiceProvider();
                        dES.Mode = CipherMode.CBC;
                        if (bytesKey == null)
                        {
                            encKey = dES.Key;
                        }
                        else
                        {
                            dES.Key = bytesKey;
                            encKey = dES.Key;
                        }

                        if (initVec == null)
                        {
                            initVec = dES.IV;
                        }
                        else
                        {
                            dES.IV = initVec;
                        }

                        return dES.CreateEncryptor();
                    }
                case EncryptionAlgorithm.TripleDes:
                    {
                        TripleDES tripleDES = new TripleDESCryptoServiceProvider();
                        tripleDES.Mode = CipherMode.CBC;
                        if (bytesKey == null)
                        {
                            encKey = tripleDES.Key;
                        }
                        else
                        {
                            tripleDES.Key = bytesKey;
                            encKey = tripleDES.Key;
                        }

                        if (initVec == null)
                        {
                            initVec = tripleDES.IV;
                        }
                        else
                        {
                            tripleDES.IV = initVec;
                        }

                        return tripleDES.CreateEncryptor();
                    }
                case EncryptionAlgorithm.Rc2:
                    {
                        RC2 rC = new RC2CryptoServiceProvider();
                        rC.Mode = CipherMode.CBC;
                        if (bytesKey == null)
                        {
                            encKey = rC.Key;
                        }
                        else
                        {
                            rC.Key = bytesKey;
                            encKey = rC.Key;
                        }

                        if (initVec == null)
                        {
                            initVec = rC.IV;
                        }
                        else
                        {
                            rC.IV = initVec;
                        }

                        return rC.CreateEncryptor();
                    }
                case EncryptionAlgorithm.Rijndael:
                    {
                        Rijndael rijndael = new RijndaelManaged();
                        rijndael.Mode = CipherMode.CBC;
                        if (bytesKey == null)
                        {
                            encKey = rijndael.Key;
                        }
                        else
                        {
                            rijndael.Key = bytesKey;
                            encKey = rijndael.Key;
                        }

                        if (initVec == null)
                        {
                            initVec = rijndael.IV;
                        }
                        else
                        {
                            rijndael.IV = initVec;
                        }

                        return rijndael.CreateEncryptor();
                    }
                default:
                    throw new CryptographicException(string.Concat("Algorithm ID '", algorithmID, "' not supported."));
            }
        }
    }

    public enum EncryptionAlgorithm
    {
        Des = 1,
        Rc2,
        Rijndael,
        TripleDes
    }



}
