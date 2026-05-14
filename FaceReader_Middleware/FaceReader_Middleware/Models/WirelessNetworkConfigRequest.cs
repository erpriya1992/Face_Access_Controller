namespace FaceReader_Middleware.Models
{
    public class WirelessNetworkConfigRequest
    {
        public string Pass { get; set; }

        // Wi-Fi SSID
        public string SsId { get; set; }

        // Wi-Fi password
        public string Pwd { get; set; }

        // true = DHCP, false = Static IP
        public bool IsDHCPMod { get; set; }

        public string Ip { get; set; }

        public string Gateway { get; set; }

        public string SubnetMask { get; set; }

        public string Dns { get; set; }
    }
}

