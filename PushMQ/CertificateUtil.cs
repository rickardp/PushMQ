using System;
using System.Text;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PushMQ {
	/// <summary>
	/// Utility methods to support client side certificates to be specified in a flexible way.
	/// 
	/// Supported formats:
	/// * PKCS12 blob with both certificate and private key (in this case, key can be null)
	/// * CER (DER) file with DER or PEM private key in separate byte array
	/// * PEM file with DER or PEM private key in separate byte array
	/// 
	/// Password-protected keys are not supported.
	/// </summary>
	public class CertificateUtil {
		public static X509Certificate2 DecodeCertificate (byte[] cert, byte[] key) {
			var value = new X509Certificate2 (cert);
			if (!value.HasPrivateKey && key != null) {
				value.PrivateKey = DecodePrivateKey (key);
			}
			if (!value.HasPrivateKey) {
				throw new ArgumentException (string.Format ("No private key specified for certificate {0}", value));
			}
			return value;
		}

		public static RSA DecodePrivateKey (byte[] key) {
			if (key.Length > 100 && key [0] == '-' && key [1] == '-') {
				// Check if key is a PEM private key
				var pemString = Encoding.ASCII.GetString (key);
				var pemData = DecodePEM (pemString, "RSA PRIVATE KEY");
				if (pemData != null) {
					var rsa = DecodeRSAPrivateKey (pemData);
					if (rsa != null)
						return rsa;
				}
			}
			// Check if key is a DER private key
			try {
				var rsa = DecodeRSAPrivateKey (key);
				if (rsa != null) {
					return rsa;
				}
			} catch (Exception) {
				throw;
			}
			// Check if key is embedded in a certificate
			try {
				var keyValue = new X509Certificate2 (key);
				if (keyValue.HasPrivateKey) {
					var rsa = keyValue.PrivateKey as RSA;
					if (rsa != null) {
						return rsa;
					}
				}
			} catch (Exception) {
			}
			throw new ArgumentException ("Private key could not be decoded");
		}

		private static byte[] DecodePEM (string pemstr, string section) {
			using (var bytes = new MemoryStream ()) {
				bool started = false;
				var sectionStart = "---BEGIN " + section.ToUpper ();
				foreach (var k in pemstr.Split ('\n')) {
					if (k.Contains (sectionStart)) {
						started = true;
					} else if (started) {
						if (k.Contains ("---END")) {
							break;
						} else if (!k.Contains (":")) {
							var b = Convert.FromBase64String (k);
							bytes.Write (b, 0, b.Length);
						} else if (k.Contains ("ENCRYPTED")) {
							throw new NotImplementedException ("Encrypted private keys are not supported");
						}
					}
				}
				if (bytes.Length > 0) {
					return bytes.GetBuffer ();
				}
			}
			return null;
		}

		private static RSACryptoServiceProvider DecodeRSAPrivateKey (byte[] privkey) {
			byte[] MODULUS, E, D, P, Q, DP, DQ, IQ;

			using (BinaryReader binr = new BinaryReader (new MemoryStream (privkey))) {
				byte bt = 0;
				ushort twobytes = 0;

				twobytes = binr.ReadUInt16 ();
				if (twobytes == 0x8130)	//data read as little endian order (actual data order for Sequence is 30 81)
					binr.ReadByte ();	//advance 1 byte
				else if (twobytes == 0x8230)
					binr.ReadInt16 ();	//advance 2 bytes
				else
					return null;

				twobytes = binr.ReadUInt16 ();
				if (twobytes != 0x0102)	//version number
					return null;
				bt = binr.ReadByte ();
				if (bt != 0x00)
					return null;

				MODULUS = ReadChunk (binr);

				E = ReadChunk (binr);
				D = ReadChunk (binr);
				P = ReadChunk (binr);
				Q = ReadChunk (binr);
				DP = ReadChunk (binr);
				DQ = ReadChunk (binr);
				IQ = ReadChunk (binr);

				RSACryptoServiceProvider RSA = new RSACryptoServiceProvider ();
				RSAParameters RSAparams = new RSAParameters ();
				RSAparams.Modulus = MODULUS;
				RSAparams.Exponent = E;
				RSAparams.D = D;
				RSAparams.P = P;
				RSAparams.Q = Q;
				RSAparams.DP = DP;
				RSAparams.DQ = DQ;
				RSAparams.InverseQ = IQ;
				RSA.ImportParameters (RSAparams);
				return RSA;
			}
		}

		private static byte[] ReadChunk (BinaryReader binr) {
			byte bt = 0;
			byte lowbyte = 0x00;
			byte highbyte = 0x00;
			int count = 0;
			bt = binr.ReadByte ();
			if (bt != 0x02)		//expect integer
				return null;
			bt = binr.ReadByte ();

			if (bt == 0x81)
				count = binr.ReadByte ();	// data size in next byte
			else if (bt == 0x82) {
				highbyte = binr.ReadByte ();	// data size in next 2 bytes
				lowbyte = binr.ReadByte ();
				byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
				count = BitConverter.ToInt32 (modint, 0);
			} else {
				count = bt;		// we already have the data size
			}

			while (binr.ReadByte () == 0x00) {	//remove high order zeros in data
				count -= 1;
			}
			binr.BaseStream.Seek (-1, SeekOrigin.Current);		//last ReadByte wasn't a removed zero, so back up a byte
			return binr.ReadBytes (count);
		}
	}
}

