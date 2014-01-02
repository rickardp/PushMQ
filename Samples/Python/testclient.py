#!/usr/bin/env python
#
# This program requires the pika client library, install with "sudo pip install pika".
from __future__ import print_function
import sys, json, uuid
from base64 import b64encode
from optparse import OptionParser
import pika

parser = OptionParser()
parser.add_option("--host", dest="host",
                  help="The broker host name.", metavar="HOSTNAME", default="localhost")
parser.add_option("--port", dest="port",
                  help="The broker port.", metavar="NUMBER", default=5672, type=int)
parser.add_option("--username", dest="username",
                  help="The broker user name.", metavar="USERNAME", default="guest")
parser.add_option("--password", dest="password",
                  help="The broker password.", metavar="PASSWORD", default="guest")
parser.add_option("--virtual-host", dest="virtualhost",
                  help="The broker virtual host.", default="/")
parser.add_option("-m", "--message", dest="message",
                  help="The message body to send.", metavar="STRING", default="Empty message")
parser.add_option("-q", "--quiet", dest="quiet",
                  help="Sends the message without waiting for a result.", action="store_true")
parser.add_option("-c", "--certfile", dest="certfile",
                  help="The certificate file name.", metavar="FILENAME")
parser.add_option("-k", "--keyfile", dest="keyfile",
                  help="The private key file name (unless certificate already contains a private key).", metavar="FILENAME")
parser.add_option("-t", "--device-tokens", dest="devicetokens",
                  help="Comma-separated list of device tokens.", metavar="TOKEN1[,TOKEN2,...]")

(options, args) = parser.parse_args()

if not options.certfile:
    parser.error("Must specify certificate file")
    
if not options.devicetokens:
    parser.error("Must specify at least one device token")

# Read key and cert
key = None
if options.keyfile: key = b64encode(file(options.keyfile).read())
cert = b64encode(file(options.certfile).read())

# Build the message
recipients = []
for device_token in options.devicetokens.split(','):
    recipients.append({
        'device_token' : device_token,
        'type' : 'apple',
        'key' : key,
        'cert' : cert
    })
msg = {
    'notification' : {
        'message' : options.message
    },
    'recipients' : recipients
}

# Connect to RabbitMQ and send the message
connection = pika.BlockingConnection(pika.ConnectionParameters(options.host, options.port, 
    credentials=pika.PlainCredentials(options.username, options.password)))
channel = connection.channel()
channel.queue_declare(queue='push', durable=True, exclusive=False, auto_delete=False)
reply_channel = None
props = None

if not options.quiet:
    reply_channel = channel.queue_declare()
    props = pika.BasicProperties(correlation_id = str(uuid.uuid4()),
                                 reply_to = reply_channel.method.queue)
    def received_response(channel, method, rprops, body):
        if props.correlation_id == rprops.correlation_id:
            print("Response",channel,method,props,body)
            reply_channel = None
            sys.exit(0)
    print("Sent message, waiting for result")
    channel.basic_consume(received_response, no_ack=True, queue=reply_channel.method.queue)
    
channel.basic_publish(exchange='',
                      routing_key='push',
                      body=json.dumps(msg),
                      properties=props)
while reply_channel:
    connection.process_data_events()

