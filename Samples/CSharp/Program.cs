using System;
using RabbitMQ.Client;
using CommandLine;
using CommandLine.Text;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace TestClient {
	class Options {
		[Option ("host", DefaultValue = "localhost",
			HelpText = "The broker host name.")]
		public string Host { get; set; }

		[Option ("port", DefaultValue = 5672,
			HelpText = "The broker port.")]
		public int Port { get; set; }

		[Option ("username", DefaultValue = "guest",
			HelpText = "The broker user name.")]
		public string UserName { get; set; }

		[Option ("password", DefaultValue = "guest",
			HelpText = "The broker password.")]
		public string Password { get; set; }

		[Option ("virtual-host", DefaultValue = "/",
			HelpText = "The broker virtual host.")]
		public string VirtualHost { get; set; }

		[Option ("protocol", DefaultValue = "AMQP_0_9",
			HelpText = "The AMQP protocol version.")]
		public string ProtocolVersion { get; set; }

		[Option ('m', "message", DefaultValue = "Empty message")]
		public string Message { get; set; }

		[Option ('c', "certfile", Required = true)]
		public string CertFile { get; set; }

		[Option ('k', "keyfile")]
		public string KeyFile { get; set; }

		[OptionList ('t', "device-tokens", Required = true, Separator = ',', HelpText = "Comma-separated list of tokens")]
		public IList<string> DeviceTokens { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }

		[HelpOption]
		public string GetUsage () {
			return HelpText.AutoBuild (this,
				(HelpText current) => HelpText.DefaultParsingErrorsHandler (this, current));
		}
	}

	class MainClass {
		public static void Main (string[] args) {
			var options = new Options ();
			if (CommandLine.Parser.Default.ParseArguments (args, options)) {
				var factory = new ConnectionFactory {
					UserName = options.UserName,
					Password = options.Password,
					HostName = options.Host,
					Port = options.Port,
					VirtualHost = options.VirtualHost,
					Protocol = Protocols.SafeLookup (options.ProtocolVersion)
				};

				using (var conn = factory.CreateConnection ()) {
					using (var channel = conn.CreateModel ()) {
						channel.QueueDeclare ("push", true, false, false, null);
						var replyChannel = channel.QueueDeclare ();
						var consumer = new QueueingBasicConsumer (channel);
						channel.BasicConsume (replyChannel.QueueName, true, consumer);

						byte[] key = null, cert;
						if (options.KeyFile != null) {
							using (var file = new FileStream (options.KeyFile, FileMode.Open, FileAccess.Read)) {
								key = new byte[file.Length];
								file.Read (key, 0, (int)file.Length);
							}
						}
						using (var file = new FileStream (options.CertFile, FileMode.Open, FileAccess.Read)) {
							cert = new byte[file.Length];
							file.Read (cert, 0, (int)file.Length);
						}

						var props = channel.CreateBasicProperties ();
						props.ReplyTo = replyChannel.QueueName;
						props.CorrelationId = Guid.NewGuid ().ToString ();
						var body = new JObject ();
						body ["notification"] = new JObject ();
						body ["notification"] ["message"] = options.Message;
						var recipients = new JArray ();
						body ["recipients"] = recipients;
						foreach (var token in options.DeviceTokens) {
							var recipient = new JObject ();
							recipient ["device_token"] = token;
							recipient ["type"] = "apple";

							recipient ["key"] = key;
							recipient ["cert"] = cert;
							recipients.Add (recipient);
							Console.WriteLine (token);
						}
						channel.BasicPublish ("", "push", props, Encoding.UTF8.GetBytes (body.ToString ()));

						while (true) {
							var ea = consumer.Queue.Dequeue ();
							if (ea.BasicProperties.CorrelationId == props.CorrelationId) {
								Console.WriteLine (Encoding.UTF8.GetString (ea.Body));
								break;
							}
						}
					}
				}
			}
		}
	}
}
