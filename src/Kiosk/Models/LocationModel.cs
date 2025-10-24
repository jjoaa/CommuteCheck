using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

public class LocationModel : INotifyPropertyChanged
{
    private bool _isSelected;

    [JsonProperty("host_location_oid")] public long HostLocationOid { get; set; }

    [JsonProperty("location_name")] public string LocationName { get; set; }

    //[JsonProperty("image_url")]
    //public string ImageUrl { get; set; }

    [JsonProperty("major_id")] //beacon
    public int MajorId { get; set; }

    [JsonProperty("minor_id")] //beacon
    public int MinorId { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}