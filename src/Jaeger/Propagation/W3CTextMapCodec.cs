using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTracing.Propagation;

namespace Jaeger.Propagation
{
    public class W3CTextMapCodec : Codec<ITextMap>
    {
        public const string TraceParentName = "traceparent";
        public const string TraceStateName = "tracestate";
        public const string TraceStateTracingSystemName = "jaeger";

        public const string Version = "00";
        public const int VersionSize = 2;

        public const char Delimiter = '-';

        public const int DelimiterSize = 1;
        public const int TraceIdSize = 2 * 16;
        public const int SpanIdSize = 2 * 8;
        public const int TraceFlagsSize = 2;
        public const int MaxTraceStates = 32;

        public const int TraceIdOffset = VersionSize + DelimiterSize;
        public const int SpanIdOffest = TraceIdOffset + TraceIdSize + DelimiterSize;
        public const int TraceFlagsOffset = SpanIdOffest + SpanIdSize + DelimiterSize;

        public const int TraceParentHeaderSize = TraceFlagsOffset + TraceFlagsSize;

        protected override SpanContext Extract(ITextMap carrier)
        {
            string traceParent = null;
            string debugId = null;

            foreach (var entry in carrier)
            {
                if (string.Equals(entry.Key, TraceParentName, StringComparison.OrdinalIgnoreCase))
                {
                    traceParent = entry.Value;
                }
                else if (string.Equals(entry.Key, Constants.DebugIdHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    debugId = entry.Value;
                }
            }
            if (traceParent == null)
            {
                if (debugId != null)
                {
                    return SpanContext.WithDebugId(debugId);
                }
                return null;
            }

            return GetContextFromParent(traceParent);
        }

        private SpanContext GetContextFromParent(string traceParent)
        {
            if (ValidateTraceParent(traceParent))
            {
                var traceId = TraceId.FromString(traceParent.Substring(TraceIdOffset, TraceIdSize));
                var spanId = SpanId.FromString(traceParent.Substring(SpanIdOffest, SpanIdSize));
                var flags = ushort.Parse(traceParent.Substring(TraceFlagsOffset, TraceFlagsSize));

                return new SpanContext(traceId, SpanId.NewUniqueId(), spanId, flags == 1 ? SpanContextFlags.Sampled : SpanContextFlags.None);
            }
            return null;
        }

        private bool ValidateTraceParent(string traceParent)
        {
            return (
                traceParent.Length == TraceParentHeaderSize ||
                (traceParent.Length > TraceParentHeaderSize && traceParent[TraceParentHeaderSize] == '-')
            )
            && traceParent[TraceIdOffset - 1] == Delimiter
            && traceParent[SpanIdOffest - 1] == Delimiter
            && traceParent[TraceFlagsOffset - 1] == Delimiter;
        }

        protected override void Inject(SpanContext spanContext, ITextMap carrier)
        {
            string traceParent = $"{Version}{Delimiter}{spanContext.TraceId.ToString()}{Delimiter}" +
                $"{spanContext.SpanId.ToString()}{Delimiter}" +
                $"0{(spanContext.IsSampled ? '1' : '0')}";
            carrier.Set(TraceParentName, traceParent);

            string[] incommingTraceStates = null;
            foreach (var entry in carrier)
            {
                if (string.Equals(entry.Key, TraceStateName, StringComparison.OrdinalIgnoreCase))
                {
                    incommingTraceStates = entry.Value.Split(',');
                }
            }

            if (incommingTraceStates.Length < MaxTraceStates)
            {
                var spanContexString = spanContext.ContextAsString();
                if (TraceStateName.Length + spanContexString.Length <= 256)
                {
                    var outGoingTraceStates = new List<string> { $"{TraceStateName}={spanContexString}" };
                    if (incommingTraceStates != null)
                    {
                        foreach (var ts in incommingTraceStates)
                        {
                            if (!ts.StartsWith(TraceStateTracingSystemName))
                            {
                                outGoingTraceStates.Add(ts);
                            }
                        }
                    }
                    carrier.Set(TraceStateName, string.Join(",", outGoingTraceStates));
                }

            }
        }

    }
}
