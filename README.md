PushMQ
======

Robust push notification broker built on RabbitMQ and PushSharp.

The high level design goals are:

 * MQ-driven: RabbitMQ provides failure recovery, load balancing and robust messaging, as well has
   having client libraries for most major languages.
 * Service-agnostic: Should support all major push notification services. The PushSharp project provides
   support for most major services, and services are actively being added.
 * App-agnostic: Should be able to drive push notification support for many different apps and services. New
   services should be able to use a PushMQ broker with minimal initialization.
 * Language-agnostic: PushMQ is currently written in C#, but should not pose any restrictions on the language
   used for clients. The protocol should be simple enough that no client libraries need to be provided.
 * Configuration free: Should require minimal persistent state. Any configuration required should be done by the 
   client services using the documented protocol.
 * Docker friendly: Should be easy to run in a Docker (http://docker.io) container. This project will later provide
   pre-built containers.



How do I run it?
----------------

Just run `PushMQ.exe`. The default configuration assumes a local RabbitMQ broker running on the default port.

It is also possible to change the host and port for the RabbitMQ broker, e.g.:

    mono bin/Debug/PushMQ.exe --host therabbit --port 1234
    
This is the full set of command-line options

    --host            (Default: localhost) The broker host name.
    --port            (Default: 5672) The broker port.
    --username        (Default: guest) The broker user name.
    --password        (Default: guest) The broker password.
    --virtual-host    (Default: /) The broker virtual host.
    --protocol        (Default: AMQP_0_9) The AMQP protocol version.
    -v, --verbose     (Default: True) Prints all messages to standard output.
    --help            Display help.

How do I send a push notification?
----------------------------------

*Note* The protocol is currently under development and has not stabilized. It is possible that the protocol can change as development proceeds. Be advised!

To get started, it can be useful to review the example code. Currently, there is a C# example called "TestClient". It allows you to send a notification to a real
iOS device by manually noting down the registration token and providing a SSL certificate.


Supported platforms
-------------------

PushMQ is actively developed on Mac OS X and runs under Mono. Plans are to support all the major platforms,
but currently the two main platforms focused on is Mac OS X and Ubuntu Linux (but it is very likely that 
most Mono-supported platforms will work).

How do I build it?
------------------

Open the solution file in Xamarin Studio or Microsoft Visual Studio. In Xamarin/MonoDevelop, verify that the NuGet plugin is installed.

The project uses NuGet packages for its dependencies, but is currently not configured for automatic package restore. You may need to begin with restoring NuGet packages.

Build the solution and run the unit tests.

Known issues
------------

* does not currently support all services that PushSharp supports. Work is ongoing, and the effort required to add more services is small.
* does not currently support device unregistration (i.e. if user deleted the app). This is something that will be added soon.