using System.ComponentModel.DataAnnotations;

namespace Signal_Windows.Models
{
    public class SignalMessageContent
    {
        [Key]
        public long rowid { get; set; }

        public string Content { get; set; }
    }
}