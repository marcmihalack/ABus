﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ABus.Contracts;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace ABus.AzureServiceBus
{
    public class AzureBusTransport : IMessageTransport
    {
        public event EventHandler<RawMessage> MessageReceived;

        public event EventHandler<TransportException> ExceptionOccured;

        public AzureBusTransport()
        {
            this.HostInstances = new Dictionary<string, TransportInstance>();
            this.CreatedTopicClients = new Dictionary<string, TopicClient>();
            this.CreatedSubscriptionClients = new Dictionary<string, SubscriptionClient>();
        }

        Dictionary<string, TransportInstance> HostInstances { get; set; }
         
        public void ConfigureHost(TransportDefinition transport)
        {
            if (!this.HostInstances.ContainsKey(transport.Uri))
            {
                var hostInstance = new TransportInstance { Uri = transport.Uri, Credentials = transport.Credentials };
                var ns = NamespaceManager.CreateFromConnectionString(hostInstance.ConnectionString);
                hostInstance.Namespace = ns;

                this.HostInstances.Add(transport.Uri, hostInstance);
            }
        }

        public Task DeleteQueue(QueueEndpoint endpoint)
        {
            return this.HostInstances[endpoint.Host].Namespace.DeleteTopicAsync(endpoint.Name);
        }

        public Task CreateQueue(QueueEndpoint endpoint)
        {
            return this.HostInstances[endpoint.Host].Namespace.CreateTopicAsync(endpoint.Name);
        }

        public void Publish(QueueEndpoint endpoint, RawMessage message)
        {
            //var client = this.GetTopicClient(endpoint.Name);
        }

        public void Send(QueueEndpoint endpoint, RawMessage message)
        {
            this.SendAsync(endpoint, message).Wait();
        }

        public void Send(QueueEndpoint endpoint, IEnumerable<RawMessage> message)
        {
            this.SendAsync(endpoint, message).Wait();
        }

        public async Task SendAsync(QueueEndpoint endpoint, IEnumerable<RawMessage> messages)
        {
            var client = this.GetTopicClient(endpoint);

            var waitingTasks = new List<Task>();
            foreach(var raw in messages)
                waitingTasks.Add(client.SendAsync(this.ConvertToBrokeredMessage(raw)));

            await Task.WhenAll(waitingTasks.ToArray());
        }

        public Task SendAsync(QueueEndpoint endpoint, RawMessage message)
        {
            var client = this.GetTopicClient(endpoint);

            var brokeredMessage = this.ConvertToBrokeredMessage(message);
            return client.SendAsync(brokeredMessage);
        }

        public async Task SubscribeAsync(QueueEndpoint endpoint, string subscriptionName)
        {
            var client = await this.GetSubscriptionClient(endpoint, subscriptionName);
            TransportException transportException = null;

            // Configure the callback options
            var options = new OnMessageOptions
            {
                AutoComplete = false,
                AutoRenewTimeout = TimeSpan.FromMinutes(1),
                MaxConcurrentCalls = 10,
            };
            client.PrefetchCount = 100;
            client.OnMessageAsync(async message =>
            {
                bool shouldAbandon = false;
                try
                {
                    // publish the message to the lister
                    if (this.MessageReceived != null)
                    {
                        this.MessageReceived(subscriptionName, this.ConvertFromBrokeredMessage(message));
                    }

                    // Remove message from subscription
                    await message.CompleteAsync();
                }
                catch (Exception ex) 
                {
                    // Capture the exception that has occured so that it can be evented out
                    transportException = new TransportException("OnMessage pump", ex);
                    // Indicates a problem, unlock message in subscription
                    shouldAbandon = true;
                }

                if (shouldAbandon)
                {
                    // Abandon the message first
                    await message.AbandonAsync();

                    // Now event the exception
                    if (this.ExceptionOccured != null && transportException != null)
                        this.ExceptionOccured(this, transportException);
                }  

            }, options);
        }

        Dictionary<string, TopicClient> CreatedTopicClients { get; set; }
        Dictionary<string, SubscriptionClient> CreatedSubscriptionClients { get; set; }

        /// <summary>
        /// A queue as used with Azure Service Bus is a topic which allows for a pub/sub model on a queue
        /// </summary>
        /// <param name="topic"></param>
        TopicClient GetTopicClient(QueueEndpoint endpoint)
        {
            var host = this.HostInstances[endpoint.Host];
            var ns = host.Namespace;
            var topic = endpoint.Name;

            TopicClient topicClient;
            if(!this.CreatedTopicClients.TryGetValue(topic, out topicClient))
            {
                if (!ns.TopicExists(topic))
                    ns.CreateTopic(topic);

                topicClient = TopicClient.CreateFromConnectionString(host.ConnectionString, topic);

                this.CreatedTopicClients.Add(topic, topicClient);
            }

            // return the topic client that has been stored
            return topicClient;
        }

        async Task<SubscriptionClient> GetSubscriptionClient(QueueEndpoint endpoint, string subscription)
        {
            var host = this.HostInstances[endpoint.Host];
            var ns = host.Namespace;

            var topic = endpoint.Name;

            SubscriptionClient subscriptionClient;
            if (!this.CreatedSubscriptionClients.TryGetValue(subscription, out subscriptionClient))
            {
                if (!ns.TopicExists(topic))
                    throw new ArgumentException(string.Format("Unable to subscribe to topic {0} as it does not exist.", topic));

                // Verify that the error queue exists before setting up the subscription
                var errorQueue = "errors";
                if (!ns.TopicExists(errorQueue))
                    await this.CreateQueue(new QueueEndpoint { Host = endpoint.Host, Name = errorQueue });

                // Now check if the subscription already exists if not create it
                if (!ns.SubscriptionExists(topic, subscription))
                {
                    var subscriptionConfig = new SubscriptionDescription(topic, subscription)
                    {
                        DefaultMessageTimeToLive = TimeSpan.FromSeconds(86400), // 24hrs
                        LockDuration = TimeSpan.FromSeconds(300), // 5 mins
                        EnableDeadLetteringOnMessageExpiration = true,
                        EnableDeadLetteringOnFilterEvaluationExceptions = true,
                        ForwardDeadLetteredMessagesTo = errorQueue,
                    };
                    await ns.CreateSubscriptionAsync(subscriptionConfig);
                }

                subscriptionClient = SubscriptionClient.CreateFromConnectionString(host.ConnectionString, topic, subscription, ReceiveMode.PeekLock);

                this.CreatedSubscriptionClients.Add(subscription, subscriptionClient);
            }

            // return the topic client that has been stored
            return subscriptionClient;
        }

        BrokeredMessage ConvertToBrokeredMessage(RawMessage rawMessage)
        {
            var bm = new BrokeredMessage(new MemoryStream(rawMessage.Body), true);
            foreach (var m in rawMessage.MetaData)
                bm.Properties.Add(m.Name, m.Value);

            return bm;
        }

        RawMessage ConvertFromBrokeredMessage(BrokeredMessage brokeredMessage)
        {
            var msg = new RawMessage();
            msg.MessageId = brokeredMessage.MessageId;

            // Transfer meta data
            foreach(var p in brokeredMessage.Properties)
                msg.MetaData.Add(new MetaData{Name = p.Key, Value = p.Value as string});

            using (var stream = brokeredMessage.GetBody<Stream>())
            {
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, (int)stream.Length);

                // assign to message
                msg.Body = buffer;
            }

            return msg;
        }


        public Task<bool> QueueExists(QueueEndpoint endpoint)
        {
            return this.HostInstances[endpoint.Host].Namespace.TopicExistsAsync(endpoint.Name);
        }
    }

    internal class TransportInstance : TransportDefinition
    {
        public NamespaceManager Namespace { get; set; }

        public string ConnectionString { get { return string.Format("Endpoint={0};{1}", this.Uri, this.Credentials); } }

    }
}
