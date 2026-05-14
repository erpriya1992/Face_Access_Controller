using System.Collections.Generic;

namespace FaceReader_Middleware.Models
{
    public class ElevatorFloorAssignmentRequest
    {
        public string PersonId { get; set; }

        // List of floors this person is allowed to access
        public List<int> Floors { get; set; }
    }

    public class ElevatorOpenDoorRequest
    {
        public string PersonId { get; set; }

        // Floor number the user selected inside the lift panel
        public int Floor { get; set; }
    }
}

