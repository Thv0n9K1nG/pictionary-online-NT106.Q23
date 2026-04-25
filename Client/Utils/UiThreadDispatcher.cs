namespace Client.Utils;

public static class UiThreadDispatcher
{
    public static void Run(Control control, Action action)
    {
        if (control.InvokeRequired)
        {
            control.BeginInvoke(action);
            return;
        }

        action();
    }
}
