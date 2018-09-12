using System;

namespace MultiProcessBuild
{
    public class Stopwatch : IDisposable
    {
        public static long Ticks { get { return DateTime.Now.Ticks; } }
        public static float Secs { get { return (float)Ticks / Frequency; } }
        public static long Frequency { get { return 10000000L; } }

        public long StartTicks { get; private set; }
        public long EndTicks { get; private set; }
        public long UseTicks { get { return EndTicks - StartTicks; } }
        public float UseSecs { get { return (float)Ticks / Frequency; } }
        bool stoped;

        public Stopwatch()
        {
            this.StartTicks = Ticks;
        }

        public void Stop()
        {
            if (this.stoped)
                return;
            this.stoped = true;
            this.EndTicks = Ticks;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
