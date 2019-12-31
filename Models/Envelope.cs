namespace SecureHost.Models
{
    public class Envelope
    {
        public string Key { get; set; }
        public string IV { get; set; }
        public string Content { get; set; }
    }
}
