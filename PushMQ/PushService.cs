using System;
using System.Collections.Generic;
using PushSharp.Core;
using Newtonsoft.Json.Linq;
using PushSharp.Apple;
using PushSharp.Android;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using PushSharp;

namespace PushMQ {
	abstract class BoundPushNotification {
		public abstract void Send (Result.MessageResult result);
	}

	class PushNoficication<T> : BoundPushNotification where T : Notification {
		public T Notification { get; set; }

		public BrokerReference<T> RemoteBroker { get; set; }

		public override string ToString () {
			return string.Format ("[PushNoficication: {0}, broker# {1}]", Notification, RemoteBroker);
		}

		public override void Send (Result.MessageResult result) {
			Notification.Tag = result;
			RemoteBroker.Send (Notification);
		}
	}

	abstract class BrokerReference<T> where T : Notification {
		protected readonly PushBroker broker = new PushBroker ();

		public BrokerReference () {
			broker.OnNotificationSent += (object sender, INotification notification) => ((Result.MessageResult)notification.Tag).Succeed ();
			broker.OnNotificationFailed += (object sender, INotification notification, Exception error) => {
				Console.WriteLine ("Failed to send {0}: {1}", notification, error);
				((Result.MessageResult)notification.Tag).Fail (error);
			};
		}

		public void Send (T notification) {
			broker.QueueNotification (notification);
		}
	}

	class PushService {
		public BoundPushNotification Parse (JToken serializedValue, JToken recipient) {
			var type = (string)recipient ["type"];
			var token = (string)recipient ["device_token"];

			var message = (string)serializedValue ["message"];

			if (type == "apple") {
				var payload = new AppleNotificationPayload ();

				var badge = serializedValue ["badge"];
				if (badge != null) {
					payload.Badge = (int)badge;
				}
				var sound = serializedValue ["sound"];
				if (sound != null) {
					payload.Sound = (string)sound;
				}
				payload.Alert = new AppleNotificationAlert { Body = message };
				var notification = new AppleNotification (token, payload);

				byte[] cert = null, key = null;
				if (recipient ["cert"] != null && recipient ["cert"].Type != JTokenType.Null) {
					cert = (byte[])recipient ["cert"];
				} else if (serializedValue ["cert"] != null && serializedValue ["cert"].Type != JTokenType.Null) {
					cert = (byte[])serializedValue ["cert"];
				}
				if (recipient ["key"] != null && recipient ["key"].Type != JTokenType.Null) {
					key = (byte[])recipient ["key"];
				} else if (serializedValue ["key"] != null && serializedValue ["key"].Type != JTokenType.Null) {
					key = (byte[])serializedValue ["key"];
				}
				if (cert == null) {
					throw new ArgumentException ("Requires 'cert' and 'key' attributes");
				}
				return new PushNoficication<AppleNotification> {
					Notification = notification,
					RemoteBroker = AppleBroker.Get (cert, key, false)
				};
			} else if (type == "google") {
				var gcm = new GcmNotification ();
				gcm.RegistrationIds.Add (token);
				gcm.JsonData = message.ToString ();
				return new PushNoficication<GcmNotification> {
					Notification = gcm
				};
			}
			throw new ArgumentException (string.Format ("Invalid service type '{0}'", type));
		}

		private class AppleBroker : BrokerReference<AppleNotification> {
			private static List<AppleBroker> brokers = new List<AppleBroker> ();
			private X509Certificate2 cert;
			private bool isSandbox;
			private string certDesc;

			private AppleBroker (X509Certificate2 cert, bool isSandbox) {
				this.cert = cert;
				this.isSandbox = isSandbox;
				certDesc = cert.Thumbprint;
				var settings = new ApplePushChannelSettings (!this.isSandbox, this.cert);
				broker.RegisterAppleService (settings);
			}

			public static AppleBroker Get (byte[] cert, byte[] key, bool isSandbox) {
				var x509cert = CertificateUtil.DecodeCertificate (cert, key);
				var certDesc = x509cert.Thumbprint;
				foreach (var broker in brokers) {
					Console.WriteLine ("Checking {0}", broker.certDesc);
					if (certDesc.Equals (broker.certDesc) && isSandbox == broker.isSandbox) {
						return broker;
					}
				}
				var b = new AppleBroker (x509cert, isSandbox);
				Console.WriteLine ("Creating new broker {0}", b.certDesc);
				brokers.Add (b);
				return b;
			}
		}
	}
}
