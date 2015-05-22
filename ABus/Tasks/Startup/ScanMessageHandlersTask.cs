﻿using System;
using System.Linq;
using System.Reflection;
using ABus.Contracts;

namespace ABus.Tasks.Startup
{
    public class ScanMessageHandlersTask: IPipelineStartupTask
    {
        public IAssemblyResolver AssemblyResolver { get; set; }

        public ScanMessageHandlersTask(IAssemblyResolver assemblyResolver)
        {
            AssemblyResolver = assemblyResolver;
        }

        public void Invoke(PipelineContext context, Action next)
        {
            var assemblies = this.AssemblyResolver.GetAssemblies();
            var handlers = (from a in assemblies
                from t in a.GetTypes()                              // Get a list of all types within each assembly
                from i in t.GetTypeInfo().ImplementedInterfaces     // Check TypeInfo for type 
                            where i.Name == "IHandleMessage`1"      //and only select those that implement IHandler(T message)
                select t).Distinct();

            foreach (var handler in handlers)
            {
                var handlerInterfaces = handler.GetTypeInfo().ImplementedInterfaces;

                // Only deal with implementations of IHandleMessage interfaces
                var interfaces = handlerInterfaces.Where(i => i.Name == "IHandleMessage`1");
                foreach (var interfaceImplementation in interfaces)
                {
                    var registeredHandler = new RegisteredHandler();

                    // Get the message type used
                    var argumentType = interfaceImplementation.GenericTypeArguments[0];
                    var method = interfaceImplementation.GetTypeInfo().DeclaredMethods.First();

                    // Find the already found MessageType
                    var messageType = context.RegisteredMessageTypes.FirstOrDefault(t => t.MessageType.FullName == argumentType.FullName);

                    registeredHandler.MessageType = messageType;
                    registeredHandler.Method = method;
                    registeredHandler.ClassType = handler;

                    context.RegisteredHandlers.Add(registeredHandler);

                    context.Trace.Verbose(string.Format("Class: {0} handles {1} message type.", registeredHandler.ClassType.Name, registeredHandler.MessageType.FullName));
                }
            }
            next();  
        }
    } 
}
