using System;

namespace TimeToSchool.Model
{
    public class BannedEmail
    {
        public string Email { get; set; }
        public DateTime BannedAt { get; set; }
        public string BannedByEmail { get; set; }
    }
}
