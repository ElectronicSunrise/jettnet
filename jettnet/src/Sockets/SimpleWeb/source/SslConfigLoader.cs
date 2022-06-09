using System.IO;
using System.Security.Authentication;

namespace JamesFrowen.SimpleWeb
{
    public class SslConfigLoader
    {
        internal struct Cert
        {
            public string path;
            public string password;
        }

        public static SslConfig Load(bool sslEnabled, string sslCertJson, SslProtocols sslProtocols)
        {
            // dont need to load anything if ssl is not enabled
            if (!sslEnabled)
                return default;

            string certJsonPath = sslCertJson;

            Cert cert = LoadCert(certJsonPath);

            return new SslConfig(
                                 enabled: sslEnabled,
                                 sslProtocols: sslProtocols,
                                 certPath: cert.path,
                                 certPassword: cert.password
                                );
        }

        private static Cert LoadCert(string certJsonPath)
        {
            string txt = File.ReadAllText(certJsonPath);

            string[] txts = txt.Split(',');

            string pw   = txts[0].Replace("pw:", string.Empty);
            string path = txts[1].Replace("path:", string.Empty);

            Cert cert = new Cert
            {
                path     = path,
                password = pw
            };

            if (string.IsNullOrEmpty(cert.path))
            {
                throw new InvalidDataException("Cert Json didnt not contain \"path\"");
            }

            if (string.IsNullOrEmpty(cert.password))
            {
                // password can be empty
                cert.password = string.Empty;
            }

            return cert;
        }
    }
}