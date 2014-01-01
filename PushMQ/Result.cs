using System;
using System.Dynamic;
using System.Collections.Generic;
using RabbitMQ.Client.Framing.Impl.v0_9;
using RabbitMQ.Client;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Diagnostics.Contracts;

namespace PushMQ {
	public class Result {
		public enum MessageStatus{
			Pending,
			Ok,
			Error
		}

		public interface IResponder {
			void SendResponse (JObject response);
		}

		private List<MessageResult> results = new List<MessageResult> ();
		private readonly IResponder responder;
		private bool responseSent = false;
		private JObject root = new JObject ();
		private bool failed;

		public Result (IResponder responder) {
			this.responder = responder;
		}

		public MessageResult Add (string deviceToken) {
			var result = new MessageResult (deviceToken);
			results.Add (result);
			result.StatusChanged += HandleMessageEvent;
			return result;
		}

		private void HandleMessageEvent (object sender, EventArgs e) {
			if (!responseSent) {
				// Did the client ask for a response?
				if (responder != null) {
					// Are we ready to send the response?
					if (!IsPending) {
						Console.WriteLine ("Now sending result");
						responder.SendResponse ((JObject)this);
					}
				}
			}
		}

		public static explicit operator JObject (Result r) {
			if (!r.failed) {
				var array = new JArray ();
				r.root ["recipient_results"] = array;
				foreach (var result in r.results) {
					array.Add ((JObject)result);
				}
			}
			return r.root;
		}

		private bool IsPending {
			get {
				foreach (var result in results) {
					if (result.IsPending)
						return true;
				}
				return false;
			}
		}

		/// <summary>
		/// Called when sending of the entire batch failed, usually due to a client error.
		/// </summary>
		public void Fail (Exception e) {
			root ["result"] = "ERROR";
			root ["error_message"] = e.Message;
			root ["error_code"] = e.GetType ().Name;
			failed = true;
			HandleMessageEvent (this, EventArgs.Empty);
		}

		public class MessageResult {
			private JObject result = new JObject ();

			public MessageResult (string deviceToken) {
				Status = MessageStatus.Pending;
				DeviceToken = deviceToken;
			}

			public string DeviceToken { 
				get {
					return (string)result ["device_token"];
				}
				private set {
					result ["device_token"] = value;
				}
			}

			public MessageStatus Status { get; private set; }

			public bool IsPending {
				get {
					return Status == MessageStatus.Pending;
				}
			}

			public void Fail (Exception e) {
				result ["result"] = "ERROR";
				result ["error_message"] = e.Message;
				result ["error_code"] = e.GetType ().Name;
				Status = MessageStatus.Error;
				if (StatusChanged != null) {
					StatusChanged (this, EventArgs.Empty);
				}
			}

			public void Succeed () {
				result ["result"] = "OK";
				Status = MessageStatus.Ok;
				if (StatusChanged != null) {
					StatusChanged (this, EventArgs.Empty);
				}
			}

			public event EventHandler StatusChanged;

			public static explicit operator JObject (MessageResult r) {
				if (!r.IsPending) {
					return r.result;
				} else {
					throw new InvalidOperationException ("Result is still pending");
				}
			}
		}
	}
}

