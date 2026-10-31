using System;
using System.Collections.Generic;
using System.Threading;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    // Slow module lifetimes never run on the settings or hotkey thread.
    internal sealed class ModuleCoordinator : IDisposable
    {
        readonly Dictionary<string, IDisposable> active = new Dictionary<string, IDisposable>();
        readonly AutoResetEvent changed = new AutoResetEvent(false);
        readonly object gate = new object();
        Dictionary<string, Func<IDisposable>> desired = new Dictionary<string, Func<IDisposable>>();
        readonly Thread worker;
        volatile bool stopping;
        internal ModuleCoordinator()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "Island module lifecycle" };
            worker.Start();
        }
        internal void Update(Dictionary<string, Func<IDisposable>> next)
        {
            lock (gate)
                desired = next;
            changed.Set();
        }
        void Run()
        {
            while (!stopping)
            {
                changed.WaitOne(3000);
                Dictionary<string, Func<IDisposable>> next;
                lock (gate)
                    next = desired;
                foreach (string id in new List<string>(active.Keys))
                    if (stopping || !next.ContainsKey(id))
                    {
                        try
                        {
                            active[id].Dispose();
                        }
                        catch { }
                        active.Remove(id);
                    }
                if (stopping)
                    break;
                foreach (var plan in next)
                    if (!active.ContainsKey(plan.Key))
                    {
                        IDisposable created = null;
                        try
                        {
                            created = plan.Value();
                            var module = created as IIslandModule;
                            if (module != null)
                                module.Start();
                            active[plan.Key] = created;
                            Program.Log("module-" + plan.Key + ".txt", "Running");
                        }
                        catch (Exception ex) { if (created != null) try { created.Dispose(); } catch { } Program.Log("module-" + plan.Key + ".txt", "Retrying: " + ex.GetType().Name); }
                    }
            }
            foreach (var service in active.Values)
                try
                {
                    service.Dispose();
                }
                catch { }
            active.Clear();
        }
        public void Dispose()
        {
            stopping = true;
            changed.Set();
            worker.Join(10000);
        }
    }
}
