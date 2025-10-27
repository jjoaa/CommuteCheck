using Kiosk.Pages;
using Kiosk.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Kiosk.Services.Interface
{
    public interface INavigationService
    {
        void NavigateTo<TViewModel>(object parameter = null);
    }
}