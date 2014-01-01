using NUnit.Framework;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.ConstrainedExecution;
using PushMQ;

namespace PushMQ.Tests {
	[TestFixture]
	public class CertificateLoadingTest {
		private byte[] ReadCertFile (string name) {
			using (var fs = new FileStream ("../../Keys/" + name, FileMode.Open, FileAccess.Read)) {
				var ret = new byte[fs.Length];
				fs.Read (ret, 0, ret.Length);
				return ret;
			}
		}

		[Test]
		[ExpectedException (typeof(ArgumentException))]
		public void TestLoadSimpleCERCertFailsBecauseOfMissingPrivateKey () {
			var data = ReadCertFile ("certificate.cer");
			CertificateUtil.DecodeCertificate (data, null);
		}

		[Test]
		public void TestLoadPKCS12CertWithKey () {
			var certData = ReadCertFile ("certificate.p12");
			var cert = CertificateUtil.DecodeCertificate (certData, null);

			Assert.AreEqual ("Unit Test", cert.GetNameInfo (X509NameType.SimpleName, false));
			Assert.IsTrue (cert.HasPrivateKey);
		}

		[Test]
		public void TestLoadCERCertWithSeparatePEMKey () {
			var certData = ReadCertFile ("certificate.cer");
			var keyData = ReadCertFile ("private_key.pem");
			var cert = CertificateUtil.DecodeCertificate (certData, keyData);

			Assert.AreEqual ("Unit Test", cert.GetNameInfo (X509NameType.SimpleName, false));
			Assert.IsTrue (cert.HasPrivateKey);
		}

		[Test]
		[ExpectedException (typeof(NotImplementedException))]
		public void TestLoadCERCertWithSeparatePEMPasswordProtectedKeyFails () {
			var certData = ReadCertFile ("certificate.cer");
			var keyData = ReadCertFile ("private_key_1234.pem"); // Key is encrypted with "1234" as password
			var cert = CertificateUtil.DecodeCertificate (certData, keyData);

			Assert.AreEqual ("Unit Test", cert.GetNameInfo (X509NameType.SimpleName, false));
			Assert.IsTrue (cert.HasPrivateKey);
		}

		[Test]
		public void TestLoadCERCertWithSeparateDERKey () {
			var certData = ReadCertFile ("certificate.cer");
			var keyData = ReadCertFile ("private_key.der");
			var cert = CertificateUtil.DecodeCertificate (certData, keyData);

			Assert.AreEqual ("Unit Test", cert.GetNameInfo (X509NameType.SimpleName, false));
			Assert.IsTrue (cert.HasPrivateKey);
		}

		[Test]
		public void TestLoadPEMCertWithSeparateDERKey () {
			var certData = ReadCertFile ("certificate.pem");
			var keyData = ReadCertFile ("private_key.der");
			var cert = CertificateUtil.DecodeCertificate (certData, keyData);

			Assert.AreEqual ("Unit Test", cert.GetNameInfo (X509NameType.SimpleName, false));
			Assert.IsTrue (cert.HasPrivateKey);
		}

		[Test]
		public void TestLoadCERCertWithSeparateKeyInPKCS12CertWithKey () {
			var certData = ReadCertFile ("certificate.cer");
			var keyData = ReadCertFile ("certificate.p12");
			var cert = CertificateUtil.DecodeCertificate (certData, keyData);

			Assert.AreEqual ("Unit Test", cert.GetNameInfo (X509NameType.SimpleName, false));
			Assert.IsTrue (cert.HasPrivateKey);
		}
	}
}

