﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ABus.Contracts
{
    public interface IMessageTransport
    {
        event EventHandler<RawMessage> MessageReceived;

        void ConfigureHost(TransportDefinition transport);

        /// <summary>
        /// Publishes an event message to the Endpoint
        /// </summary>
        /// <param name="message"></param>
        void Publish(QueueEndpoint endpoint, RawMessage message);

        /// <summary>
        /// Sends a command message to the Endpoint
        /// </summary>
        /// <param name="message"></param>
        void Send(QueueEndpoint endpoint, RawMessage message);

        void Send(QueueEndpoint endpoint, IEnumerable<RawMessage> message);

        /// <summary>
        /// Sends a command message to the Endpoint
        /// </summary>
        /// <param name="message"></param> 
        Task SendAsync(QueueEndpoint endpoint, RawMessage message);

        /// <summary>
        /// Sends a command message to the Endpoint
        /// </summary>
        /// <param name="message"></param>
        Task SendAsync(QueueEndpoint endpoint, IEnumerable<RawMessage> message);

        /// <summary>
        /// Create a subsription on the Endpoint
        /// </summary>
        /// <param name="subscriptionName">The unique name for this subscription</param>
        Task SubscribeAsync(QueueEndpoint endpoint, string subscriptionName);

        /// <summary>
        /// Create a queue with the supplied name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task CreateQueue(QueueEndpoint endpoint);

        /// <summary>
        /// Delete a queue with the supplied name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task DeleteQueue(QueueEndpoint endpoint);

        /// <summary>
        /// Determines if a particular queue exists
        /// </summary>
        /// <param name="queue"></param>
        /// <returns></returns>
        Task<bool> QueueExists(QueueEndpoint endpoint);
    }
}
