using System;
namespace WinDynamicIsland.Core
{
    public sealed class VolumeGesture
    {
        DateTime previous; int count;
        public bool Wide
        {
            get; private set;
        }
        public bool Change(DateTime now)
        {
            if ((now - previous).TotalMilliseconds > 650)
            {
                count = 0;
                Wide = false;
            }
            previous = now;
            if (++count >= 3)
                Wide = true;
            return Wide;
        }
    }
}
