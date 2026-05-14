namespace FaceReader_Middleware.Models
{
    public class WiredNetworkConfigRequest
    {
        public string Pass { get; set; }

        /// <summary>
        /// 1 = DHCP, 2 = Static IP
        /// </summary>
        public int IsDHCPMod { get; set; }

        public string Ip { get; set; }

        public string Gateway { get; set; }

        public string SubnetMask { get; set; }

        public string Dns { get; set; }
    }
}

