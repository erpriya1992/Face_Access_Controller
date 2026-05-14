namespace FaceReader_Middleware.Models
{
    public class RemoteControlOutputRequest
    {
        public string Pass { get; set; }

        /// <summary>
        /// Interaction type of device:
        /// 1: Relay opening (default)
        /// 2: 232 serial port
        /// 3: Wiegand
        /// 4: Custom text pop-up / voice broadcast
        /// 5: 485 serial port
        /// </summary>
        public int? Type { get; set; }

        /// <summary>
        /// Output content, semantics depend on Type.
        /// </summary>
        public string Content { get; set; }
    }
}

