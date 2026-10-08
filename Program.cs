namespace TomatoClock
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using var instance = new SingleInstance();
            if (!instance.IsPrimary)
            {
                instance.RequestActivation();
                return;
            }
            ApplicationConfiguration.Initialize();
            using var form = new Form1();
            // Create a UI handle before listening; requests remain signaled during startup.
            _ = form.Handle;
            instance.Listen(() =>
            {
                try
                {
                    if (!form.IsDisposed && form.IsHandleCreated)
                        form.BeginInvoke(() => { if (!form.IsDisposed) form.ShowExisting(); });
                }
                catch (InvalidOperationException) { /* Window closed while an activation was queued. */ }
            });
            Application.Run(form);
        }
    }
}
