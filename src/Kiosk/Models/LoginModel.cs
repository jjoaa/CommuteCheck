using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

//LoginModel.cs
namespace Kiosk.Models
{
    public class LoginModel : INotifyPropertyChanged
    {
        private string _username;
        private string _password;
        private bool _isAdmin;

        public string Username 
        { 
            get => _username; 
            set 
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public string Password 
        { 
            get => _password; 
            set 
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        public bool IsAdmin 
        { 
            get => _isAdmin; 
            set 
            {
                _isAdmin = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 