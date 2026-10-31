using WinDynamicIsland.Core;
using System;

// A local clock keeps the timeline moving between Windows updates. Pending
// seeks stay on screen until the player acknowledges them, or a short timeout.
namespace WinDynamicIsland
{
    internal sealed class MediaPresentation
    {
        MediaSnapshot snapshot;
        DateTime received, pendingAt;
        double pendingPosition;
        bool pending;
        internal void Accept(MediaSnapshot next, DateTime now)
        {
            if (snapshot == null || snapshot.Id != next.Id)
                pending = false;
            if (pending)
            {
                double expected = pendingPosition + (next.Playing ? (now - pendingAt).TotalSeconds * next.Rate : 0);
                if (Math.Abs(next.Position - expected) < 1.5 || (now - pendingAt).TotalSeconds > 2)
                    pending = false;
            }
            snapshot = next;
            received = now;
        }
        internal void Seek(double position, DateTime now)
        {
            if (snapshot == null)
                return;
            pendingPosition = Clamp(position);
            pendingAt = now;
            pending = true;
        }
        internal double PositionAt(DateTime now)
        {
            if (snapshot == null)
                return 0;
            double position = pending ? pendingPosition : snapshot.Position;
            var basis = pending ? pendingAt : received;
            if (snapshot.Playing)
                position += Math.Max(0, (now - basis).TotalSeconds) * snapshot.Rate;
            return Clamp(position);
        }
        double Clamp(double value)
        {
            return Math.Max(snapshot.Start, snapshot.End > snapshot.Start ? Math.Min(snapshot.End, value) : value);
        }
    }

    internal sealed class MediaTimelineClock
    {
        string id; double rawPosition, position, rate; DateTime anchor; DateTimeOffset rawStamp; bool playing;
        bool seeking; DateTime seekAt;
        internal void Reset()
        {
            id = null;
            seeking = false;
        }
        internal void Seek(string source, double target, DateTime now)
        {
            if (source != id)
                return;
            seeking = true;
            position = target;
            anchor = seekAt = now;
        }
        internal double Read(string source, double raw, DateTimeOffset stamp, bool isPlaying, double speed, DateTime now)
        {
            // Some players return a live Position while retaining LastUpdatedTime.
            // Adding the entire timestamp age double-counts playback for these apps.
            if (seeking && source == id)
            {
                double expected = position + (playing ? Math.Max(0, (now - anchor).TotalSeconds) * rate : 0);
                bool nativeChanged = raw != rawPosition || stamp != rawStamp;
                if (Math.Abs(raw - expected) < 1.5 || nativeChanged && (now - seekAt).TotalSeconds > 2)
                    seeking = false;
                else
                {
                    if (isPlaying != playing || speed != rate)
                    {
                        position = expected;
                        anchor = now;
                        playing = isPlaying;
                        rate = speed;
                    }
                    rawPosition = raw;
                    rawStamp = stamp;
                    return position + (playing ? Math.Max(0, (now - anchor).TotalSeconds) * rate : 0);
                }
            }
            if (source != id || raw != rawPosition || stamp != rawStamp || isPlaying != playing || speed != rate)
            {
                if (source != id)
                    seeking = false;
                id = source;
                rawPosition = raw;
                rawStamp = stamp;
                position = raw;
                anchor = now;
                playing = isPlaying;
                rate = speed;
            }
            return position + (playing ? Math.Max(0, (now - anchor).TotalSeconds) * rate : 0);
        }
    }

}
