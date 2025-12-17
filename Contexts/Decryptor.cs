using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace INVOICE_VENDER_API.Contexts
{
    internal class Decryptor
    {
        private DecryptTransformer transformer;

        private byte[] initVec;

        public byte[] IV
        {
            set
            {
                initVec = value;
            }
        }

        public Decryptor(EncryptionAlgorithm algId)
        {
            transformer = new DecryptTransformer(algId);
        }

        public byte[] Decrypt(byte[] bytesData, byte[] bytesKey)
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

            cryptoStream.FlushFinalBlock();
            cryptoStream.Close();
            return memoryStream.ToArray();
        }
    }

    internal class DecryptTransformer
    {
        private EncryptionAlgorithm algorithmID;

        private byte[] initVec;

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

        internal DecryptTransformer(EncryptionAlgorithm deCryptId)
        {
            algorithmID = deCryptId;
        }

        public ICryptoTransform GetCryptoServiceProvider(byte[] bytesKey, byte[] initVec)
        {
            switch (algorithmID)
            {
                case EncryptionAlgorithm.Des:
                    {
                        DES dES = new DESCryptoServiceProvider();
                        dES.Mode = CipherMode.CBC;
                        dES.Key = bytesKey;
                        dES.IV = initVec;
                        return dES.CreateDecryptor();
                    }
                case EncryptionAlgorithm.TripleDes:
                    {
                        TripleDES tripleDES = new TripleDESCryptoServiceProvider();
                        tripleDES.Mode = CipherMode.CBC;
                        return tripleDES.CreateDecryptor(bytesKey, initVec);
                    }
                case EncryptionAlgorithm.Rc2:
                    {
                        RC2 rC = new RC2CryptoServiceProvider();
                        rC.Mode = CipherMode.CBC;
                        return rC.CreateDecryptor(bytesKey, initVec);
                    }
                case EncryptionAlgorithm.Rijndael:
                    {
                        Rijndael rijndael = new RijndaelManaged();
                        rijndael.Mode = CipherMode.CBC;
                        return rijndael.CreateDecryptor(bytesKey, initVec);
                    }
                default:
                    throw new CryptographicException(string.Concat("Algorithm ID '", algorithmID, "' not supported."));
            }
        }
    }

}
