namespace FaceReader_Middleware.Models
{
    public class PersonInfo
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string IdcardNum { get; set; }

        public string IDNumber { get; set; }

        public int? FacePermission { get; set; } = new int?(2);

        public int? IdCardPermission { get; set; } = new int?(2);

        public int? IDNumberPermission { get; set; } = new int?(2);

        public int? PasswordPermission { get; set; } = new int?(1);

        public int? FingerPermission { get; set; } = new int?(1);

        public int? QrCodePermission { get; set; } = new int?(1);

        public int? FaceAndCardPermission { get; set; } = new int?(1);

        public int? CardAndPasswordPermission { get; set; } = new int?(1);

        public int? FaceAndQrCodePermission { get; set; } = new int?(1);

        public int? FaceAndFingerPermission { get; set; } = new int?(1);

        public int? FaceAndPasswordPermission { get; set; } = new int?(1);

        public int? FingerAndPasswordPermission { get; set; } = new int?(1);

        public string QrCode { get; set; }

        public string Password { get; set; }

        public string Phone { get; set; }

        public string Tag { get; set; }

        public int? Role { get; set; } = new int?(0);

        public string ScheduleId { get; set; }

        public List<PersonRule> Rule { get; set; }
    }
}
