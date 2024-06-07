using Scarlett.ui;

namespace Scarlett;

public class Confirmation
{
    public static bool Confirm(string actionPhrase)
    {
        bool confirmed = false;
        var thread = new Thread(() =>
        {
            try
            {
                var confirmationDialog = new AudioConfirmationDialog(actionPhrase);

                confirmationDialog.Show();

                confirmationDialog.Closed += (object? sender, EventArgs e) =>
                {
                    confirmed = confirmationDialog.UserResponse;
                    System.Windows.Threading.Dispatcher.ExitAllFrames();
                };

                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception e)
            {
                Log.Print(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return confirmed;
    }
}