using NUnit.Framework;
using System;
using Newtonsoft.Json.Linq;

namespace PushMQ.Tests {
	[TestFixture]
	public class ResultHandlingTest {
		public class DummyResponder : Result.IResponder {
			public JObject Response { get; private set; }

			public int Count { get; private set; }

			public void SendResponse (JObject response) {
				Response = response;
				Count++;
			}
		}

		[Test]
		public void TestFailureReported () {
			var rsp = new DummyResponder ();
			var result = new Result (rsp);
			result.Fail (new ArgumentException ("Badness"));
			Assert.AreEqual (1, rsp.Count);
			Assert.AreEqual ("ERROR", (string)rsp.Response ["result"]);
			Assert.AreEqual ("Badness", (string)rsp.Response ["error_message"]);
			Assert.AreEqual ("ArgumentException", (string)rsp.Response ["error_code"]);
		}

		[Test]
		public void TestNoResponseWhenPending () {
			var rsp = new DummyResponder ();
			var result = new Result (rsp);
			result.Add ("Test1");
			Assert.AreEqual (0, rsp.Count);
		}

		[Test]
		public void TestFailureReportedSingleResultFailed () {
			var rsp = new DummyResponder ();
			var result = new Result (rsp);
			var x = result.Add ("Test1");
			x.Fail (new ArgumentException ("Badness"));
			Assert.AreEqual (1, rsp.Count);
			Assert.AreEqual (1, ((JArray)rsp.Response ["recipient_results"]).Count);
			Assert.AreEqual ("ERROR", (string)rsp.Response ["recipient_results"] [0] ["result"]);
			Assert.AreEqual ("Badness", (string)rsp.Response ["recipient_results"] [0] ["error_message"]);
			Assert.AreEqual ("ArgumentException", (string)rsp.Response ["recipient_results"] [0] ["error_code"]);
		}

		[Test]
		public void TestNoResponseWhenOneOfManyResultFailed () {
			var rsp = new DummyResponder ();
			var result = new Result (rsp);
			var x = result.Add ("Test1");
			result.Add ("Test2");
			x.Fail (new ArgumentException ("Badness"));
			Assert.AreEqual (0, rsp.Count);
		}

		[Test]
		public void TestNoResponseWhenAllOfManyResultCompleted () {
			var rsp = new DummyResponder ();
			var result = new Result (rsp);
			var x = result.Add ("Test1");
			var y = result.Add ("Test2");
			x.Fail (new ArgumentException ("Badness"));
			y.Succeed ();
			Assert.AreEqual (1, rsp.Count);
			Assert.AreEqual (2, ((JArray)rsp.Response ["recipient_results"]).Count);
		}
	}
}

