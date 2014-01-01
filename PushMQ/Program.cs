using System;
using CommandLine;
using CommandLine.Text;
using PushSharp.Core;
using PushSharp.Apple;
using PushSharp.Android;
using System.Configuration;
using PushMQ;
using System.ComponentModel;
using RabbitMQ.Client;

namespace PushMQ.Program {
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

		[Option ('v', "verbose", DefaultValue = true,
			HelpText = "Prints all messages to standard output.")]
		public bool Verbose { get; set; }

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
				if (factory.Port < 1) {
					Console.Error.WriteLine ("Invalid port specification");
					return;
				}

				using (var conn = factory.CreateConnection ()) {

					// Values are available here
					if (options.Verbose) {
						Console.WriteLine ("Connected to broker: {0}", conn.Endpoint);
					}

					using (var channel = conn.CreateModel ()) {
						conn.AutoClose = true;

						channel.QueueDeclare ("push", true, false, false, null);
						channel.BasicQos (0, 1, false);

						//var pushBroker = new PushBroker ();

						var consumer = new PushQueueConsumer (channel);
						channel.BasicConsume ("push", true, consumer);

						try {
							while (true) {
								consumer.HandleNext ();
							}

						} finally {
							channel.Close (200, "Closing");
						}
					}
				}
			}
		}
	}
}