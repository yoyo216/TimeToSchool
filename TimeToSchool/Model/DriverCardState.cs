using System;

namespace TimeToSchool.Model
{
    public class DriverCardState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FirebaseDocumentId { get; set; }
        public ActiveBus TripData { get; set; }
        public bool IsDriving { get; set; } = false;
    }
}
