﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ABus.Host
{
    public class ColorConsoleTraceListener : ConsoleTraceListener
    {
        readonly Dictionary<TraceEventType, ConsoleColor> eventColor = new Dictionary<TraceEventType, ConsoleColor>();

        public ColorConsoleTraceListener()
        {
            this.eventColor.Add(TraceEventType.Verbose, ConsoleColor.DarkGray);

            this.eventColor.Add(TraceEventType.Information, ConsoleColor.Gray);

            this.eventColor.Add(TraceEventType.Warning, ConsoleColor.Yellow);

            this.eventColor.Add(TraceEventType.Error, ConsoleColor.DarkRed);

            this.eventColor.Add(TraceEventType.Critical, ConsoleColor.Red);

            this.eventColor.Add(TraceEventType.Start, ConsoleColor.DarkCyan);

            this.eventColor.Add(TraceEventType.Stop, ConsoleColor.DarkCyan);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            this.TraceEvent(eventCache, source, eventType, id, "{0}", message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            var originalColor = Console.ForegroundColor;

            Console.ForegroundColor = this.getEventColor(eventType, originalColor);

            base.TraceEvent(eventCache, source, eventType, id, format, args);

            Console.ForegroundColor = originalColor;
        }

        ConsoleColor getEventColor(TraceEventType eventType, ConsoleColor defaultColor)
        {
            if (!this.eventColor.ContainsKey(eventType))
            {
                return defaultColor;
            }

            return this.eventColor[eventType];
        }
    }
}