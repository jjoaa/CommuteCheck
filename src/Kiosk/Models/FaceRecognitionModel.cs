using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kiosk.Models
{
    public class FaceRecognitionModel : INotifyPropertyChanged
    {
        private long _hostLocationOid;
        public long HostLocationOid
        {
            get => _hostLocationOid;
            set { _hostLocationOid = value; OnPropertyChanged(); }
        }

        private string _locationName;
        public string LocationName
        {
            get => _locationName;
            set { _locationName = value; OnPropertyChanged(); }
        }

        private ObservableCollection<WaitUser> _waitUsers = new();
        public ObservableCollection<WaitUser> WaitUsers
        {
            get => _waitUsers;
            set { _waitUsers = value; OnPropertyChanged(); }
        }

        private DateTime _currentDateTime = DateTime.Now;
        public DateTime CurrentDateTime
        {
            get => _currentDateTime;
            set { _currentDateTime = value; OnPropertyChanged(); }
        }
        // 상태문구 + 자동 복귀(초)
        private string _statusText = "가까이/정면/조명 확인해주세요";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public int StatusAutoRevertSec { get; set; } = 0;
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}