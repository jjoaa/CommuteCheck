using Kiosk.Views;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Utils
{
    public static class BackdropHelper
    {
        public static bool? ShowDialogWithBackdrop(Window owner, Window dialog)
        {
            var backdrop = new BackdropWindow(owner);
            try
            {
                backdrop.Show();
                dialog.Owner = backdrop; //  owner → backdrop → dialog (항상 팝업이 최상단)
                return dialog.ShowDialog();
            }
            finally
            {
                backdrop.Close();
            }
        }
    }
}