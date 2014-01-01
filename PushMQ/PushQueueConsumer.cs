using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PushSharp.Core;
using Newtonsoft.Json.Linq;
using System.Text;
using RabbitMQ.Client.Framing.Impl.v0_9;
using System.ComponentModel;

namespace PushMQ {
	class PushQueueConsumer : QueueingBasicConsumer {
		private PushService service = new PushService ();

		public PushQueueConsumer (IModel channel) : base (channel) {
		}

		private class QueueResponder : Result.IResponder {
			public IModel Model { get; set; }

			public string CorrelationId { get; set; }

			public string ReplyTo { get; set; }

			public void SendResponse (JObject response) {
				var responseBytes = Encoding.UTF8.GetBytes (response.ToString ());
				var props = Model.CreateBasicProperties ();
				props.CorrelationId = CorrelationId;
				Model.BasicPublish ("", ReplyTo, props, responseBytes);
			}
		}

		public void HandleNext () {
			var ea = Queue.Dequeue ();
			var body = ea.Body;
			QueueResponder responder = null;
			if (ea.BasicProperties.CorrelationId != null && ea.BasicProperties.CorrelationId.Length > 0 && ea.BasicProperties.ReplyTo != null) {
				responder = new QueueResponder {
					Model = Model,
					CorrelationId = ea.BasicProperties.CorrelationId,
					ReplyTo = ea.BasicProperties.ReplyTo
				};
			}
			var result = new Result (responder);
			try {
				var bodyObject = JObject.Parse (Encoding.UTF8.GetString (body));
				Console.WriteLine ("Got message {0}", bodyObject);

				var notification = bodyObject ["notification"];
				if (notification == null)
					throw new ArgumentException ("Missing 'notification' node");

				var rcpt = bodyObject ["recipients"];
				if (rcpt == null)
					throw new ArgumentException ("Missing 'recipients' node");

				foreach (var recipient in rcpt) {
					var currentResult = result.Add ((string)recipient ["device_token"]);
					try {
						var parsedNotification = service.Parse (notification, recipient);
						Console.WriteLine ("Will now send {0}", parsedNotification);
						parsedNotification.Send (currentResult);
					} catch (Exception e) {
						currentResult.Fail (e);
					}
				}
			} catch (Exception e) {
				result.Fail (e);
			}
		}
	}
}