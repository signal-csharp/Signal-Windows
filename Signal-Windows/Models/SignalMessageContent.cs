using System.ComponentModel.DataAnnotations;

namespace Signal_Windows.Models
{
    public class SignalMessageContent
    {
        [Key]
        public ulong rowid { get; set; }

        public string Content { get; set; }
    }
}
