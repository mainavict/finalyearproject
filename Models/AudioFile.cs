
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace finalyearproject.Models;
public  enum  stutus 
{
    pending,
    prossesing,
    failed,
    transcribed,
    minutesgenerated,
    minutesgenerationfailed
}


public class AudioFile
{
    
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid MeetingId { get; set; } 
    public string FileName { get; set; }
    public string FilePath { get; set; }
    
    public  string FileUrl { get; set; }
    public string ContentType { get; set; }
    public long FileSize { get; set; }
    public stutus Status { get; set; } = stutus.pending;
    
    public Meeting Meeting { get; set; }
}