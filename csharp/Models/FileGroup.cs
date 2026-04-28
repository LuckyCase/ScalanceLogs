using System.Collections.ObjectModel;

namespace SyslogViewer.Models;

public class FileGroup
{
    public string                   Date        { get; set; } = "";
    public string?                  EventsFile  { get; set; }
    public ObservableCollection<FileItem> Switches { get; set; } = [];

    // Display name for the group header node
    public string Header => EventsFile ?? Date;
}

public class FileItem
{
    public string Display  { get; set; } = "";
    public string FileName { get; set; } = "";
}
